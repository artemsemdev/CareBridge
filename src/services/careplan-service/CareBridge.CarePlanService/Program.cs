using CareBridge.CarePlanService.Data;
using CareBridge.CarePlanService.Entities;
using CareBridge.CarePlanService.Handlers;
using CareBridge.Shared.Contracts.Enums;
using CareBridge.Shared.Contracts.Events;
using CareBridge.Shared.Infrastructure.Eventing;
using CareBridge.Shared.Infrastructure.Extensions;
using CareBridge.Shared.Infrastructure.HealthChecks;
using CareBridge.Shared.Infrastructure.Resilience;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddCareBridgeDefaults();

// Security: Connection string loaded from configuration (env var or Key Vault in production). Never hardcode credentials.
var carePlanDbConnectionString = builder.Configuration.GetConnectionString("CarePlanDb")!;
builder.Services.AddDbContext<CarePlanDbContext>(options =>
    options.UseSqlServer(carePlanDbConnectionString, sql => sql.EnableRetryOnFailure()));

var rabbitHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
var rabbitPort = int.Parse(builder.Configuration["RabbitMQ:Port"] ?? "5672");
var rabbitUser = builder.Configuration["RabbitMQ:User"] ?? "guest";
var rabbitPassword = builder.Configuration["RabbitMQ:Password"] ?? "guest";
builder.Services.AddHealthChecks()
    .AddCareBridgeSqlServer(carePlanDbConnectionString)
    .AddCareBridgeRabbitMQ(rabbitHost, rabbitPort, rabbitUser, rabbitPassword);

builder.Services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();
builder.Services.AddScoped<CaseCreatedHandler>();
builder.Services.AddScoped<TaskCompletedHandler>();
builder.Services.AddScoped<AppointmentCompletedHandler>();

var careGapEngineUrl = builder.Configuration["Services:CareGapEngine"] ?? "http://localhost:5040";
builder.Services.AddHttpClient("CareGapEngine", client =>
{
    client.BaseAddress = new Uri(careGapEngineUrl);
}).AddCareBridgeResilience();

builder.Services.AddSingleton(new EventConsumerOptions
{
    QueueName = "careplan-service",
    RoutingKeys = [nameof(CaseCreated), nameof(TaskCompleted), nameof(AppointmentCompleted)],
    EventTypeMap = new Dictionary<string, Type>
    {
        [nameof(CaseCreated)] = typeof(CaseCreated),
        [nameof(TaskCompleted)] = typeof(TaskCompleted),
        [nameof(AppointmentCompleted)] = typeof(AppointmentCompleted)
    }
});
builder.Services.AddScoped<IEventHandler<CaseCreated>, CaseCreatedHandler>();
builder.Services.AddScoped<IEventHandler<TaskCompleted>, TaskCompletedHandler>();
builder.Services.AddScoped<IEventHandler<AppointmentCompleted>, AppointmentCompletedHandler>();
builder.Services.AddHostedService<EventConsumerBackgroundService>();

var app = builder.Build();

app.UseCareBridgeDefaults();
app.MapCareBridgeHealthChecks();

// PHI: Returns care plan data linked to patient cases. Care plans contain clinical workflow data.
// Authorization: In production, CareCoordinator and Clinician roles can view care plans.
app.MapGet("/api/v1/care-plans", async (Guid? caseId, string? status, CarePlanDbContext db) =>
{
    if (caseId is not null)
    {
        var plan = await db.CarePlans.Include(p => p.Milestones)
            .FirstOrDefaultAsync(p => p.CaseId == caseId.Value);
        return plan is null ? Results.NotFound() : Results.Ok(ToResponse(plan));
    }

    if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<CarePlanStatus>(status, true, out var parsedStatus))
    {
        var plans = await db.CarePlans.Include(p => p.Milestones)
            .Where(p => p.Status == parsedStatus)
            .ToListAsync();
        return Results.Ok(plans.Select(ToResponse).ToList());
    }

    return Results.Problem("Either caseId or status query parameter is required.", statusCode: 400, title: "Validation Error");
});

// Authorization: In production, all authenticated clinical roles can view care plan details.
app.MapGet("/api/v1/care-plans/{planId:guid}", async (Guid planId, CarePlanDbContext db) =>
{
    var plan = await db.CarePlans.Include(p => p.Milestones)
        .FirstOrDefaultAsync(p => p.Id == planId);

    return plan is null ? Results.NotFound() : Results.Ok(ToResponse(plan));
});

// Authorization: In production, CareCoordinator and Clinician roles can update milestones.
// Audit: MilestoneCompleted event triggers immutable audit record per HIPAA §164.312(b).
app.MapMethods("/api/v1/care-plans/{planId:guid}/milestones/{milestoneId:guid}", ["PATCH"],
    async (Guid planId, Guid milestoneId, UpdateMilestoneRequest request, CarePlanDbContext db, IEventPublisher publisher, ILogger<Program> logger) =>
    {
        var plan = await db.CarePlans.Include(p => p.Milestones)
            .FirstOrDefaultAsync(p => p.Id == planId);
        if (plan is null) return Results.NotFound();

        var milestone = plan.Milestones.FirstOrDefault(m => m.Id == milestoneId);
        if (milestone is null) return Results.NotFound();

        if (milestone.Status != MilestoneStatus.Pending)
            return Results.Problem($"Cannot transition milestone from {milestone.Status}.", statusCode: 409, title: "Conflict");

        if (request.Status != MilestoneStatus.Completed && request.Status != MilestoneStatus.Skipped)
            return Results.Problem("Status must be Completed or Skipped.", statusCode: 400, title: "Validation Error");

        milestone.Status = request.Status;
        if (request.Status == MilestoneStatus.Completed)
            milestone.CompletedAt = DateTimeOffset.UtcNow;

        if (plan.Milestones.All(m => m.Status is MilestoneStatus.Completed or MilestoneStatus.Skipped))
        {
            plan.Status = CarePlanStatus.Completed;
            plan.CompletedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync();

        if (request.Status == MilestoneStatus.Completed)
        {
            // Audit: MilestoneCompleted event records care plan progress for compliance tracing.
            try
            {
                await publisher.PublishAsync(new MilestoneCompleted
                {
                    MilestoneId = milestone.Id,
                    CarePlanId = plan.Id,
                    CaseId = plan.CaseId,
                    MilestoneName = milestone.Name,
                    CompletedAt = milestone.CompletedAt!.Value
                });
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to publish MilestoneCompleted event for milestone {MilestoneId}.", milestone.Id);
            }
        }

        return Results.Ok(ToMilestoneResponse(milestone));
    });

app.Run();

static CarePlanResponse ToResponse(CarePlanEntity plan)
{
    var now = DateTimeOffset.UtcNow;
    var milestones = plan.Milestones.OrderBy(m => m.DueAt).Select(m => ToMilestoneResponse(m)).ToList();
    var completed = milestones.Count(m => m.Status == nameof(MilestoneStatus.Completed));
    var total = milestones.Count;
    return new CarePlanResponse(
        plan.Id, plan.CaseId, plan.TemplateName,
        plan.Status.ToString(), plan.ActivatedAt, plan.CompletedAt,
        milestones,
        new ProgressSummary(completed, total, total == 0 ? 0 : (int)Math.Round(completed * 100.0 / total)));
}

static MilestoneResponse ToMilestoneResponse(MilestoneEntity m)
{
    var now = DateTimeOffset.UtcNow;
    return new MilestoneResponse(
        m.Id, m.Name, m.Description, m.DueAt,
        m.Status.ToString(), m.CompletedAt,
        m.Status == MilestoneStatus.Pending && m.DueAt < now);
}

record UpdateMilestoneRequest(MilestoneStatus Status);

record MilestoneResponse(
    Guid Id,
    string Name,
    string Description,
    DateTimeOffset DueAt,
    string Status,
    DateTimeOffset? CompletedAt,
    bool IsOverdue);

record ProgressSummary(int Completed, int Total, int PercentComplete);

record CarePlanResponse(
    Guid Id,
    Guid CaseId,
    string TemplateName,
    string Status,
    DateTimeOffset ActivatedAt,
    DateTimeOffset? CompletedAt,
    List<MilestoneResponse> Milestones,
    ProgressSummary Progress);
