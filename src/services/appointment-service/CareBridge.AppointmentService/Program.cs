using System.Text;
using CareBridge.AppointmentService.Data;
using CareBridge.AppointmentService.Entities;
using CareBridge.Shared.Contracts.Enums;
using CareBridge.Shared.Contracts.Events;
using CareBridge.Shared.Infrastructure.Eventing;
using CareBridge.Shared.Infrastructure.Extensions;
using CareBridge.Shared.Infrastructure.HealthChecks;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddCareBridgeDefaults();

// Security: Connection string loaded from configuration (env var or Key Vault in production). Never hardcode credentials.
var appointmentDbConnectionString = builder.Configuration.GetConnectionString("AppointmentDb")!;
builder.Services.AddDbContext<AppointmentDbContext>(options =>
    options.UseSqlServer(appointmentDbConnectionString, sql => sql.EnableRetryOnFailure()));

builder.Services.AddHealthChecks()
    .AddCareBridgeSqlServer(appointmentDbConnectionString);

builder.Services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();

var app = builder.Build();

app.UseCareBridgeDefaults();
app.MapCareBridgeHealthChecks();

// Authorization: In production, CareCoordinator and Clinician roles can create appointments.
// Audit: AppointmentBooked event triggers audit record when status is Booked per HIPAA §164.312(b).
app.MapPost("/api/v1/appointments", async (CreateAppointmentRequest request, AppointmentDbContext db, IEventPublisher publisher, ILogger<Program> logger) =>
{
    var errors = new List<string>();
    if (request.CaseId == Guid.Empty) errors.Add("CaseId is required.");
    if (request.Notes?.Length > 1000) errors.Add("Notes cannot exceed 1000 characters.");
    if (errors.Count > 0)
        return Results.Problem(string.Join(" ", errors), statusCode: 400, title: "Validation Error");

    var now = DateTimeOffset.UtcNow;
    var appointment = new Appointment
    {
        Id = Guid.NewGuid(),
        CaseId = request.CaseId,
        Type = request.Type ?? AppointmentType.FollowUp,
        ScheduledAt = request.ScheduledAt,
        Status = request.Status ?? AppointmentStatus.Proposed,
        Notes = request.Notes,
        CreatedAt = now,
        UpdatedAt = now
    };

    db.Appointments.Add(appointment);
    await db.SaveChangesAsync();

    if (appointment.Status == AppointmentStatus.Booked)
    {
        try
        {
            await publisher.PublishAsync(new AppointmentBooked
            {
                AppointmentId = appointment.Id,
                CaseId = appointment.CaseId,
                Type = appointment.Type.ToString(),
                ScheduledAt = appointment.ScheduledAt
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish AppointmentBooked event for appointment {AppointmentId}.", appointment.Id);
        }
    }

    return Results.Created($"/api/v1/appointments/{appointment.Id}", ToResponse(appointment));
});

// GET /api/v1/appointments — list with filters
app.MapGet("/api/v1/appointments", async (
    AppointmentDbContext db,
    Guid? caseId,
    string? status,
    DateTimeOffset? from,
    DateTimeOffset? to,
    int? limit,
    string? cursor) =>
{
    var pageSize = Math.Min(limit ?? 20, 100);
    var query = db.Appointments.AsQueryable();

    if (caseId.HasValue)
        query = query.Where(a => a.CaseId == caseId.Value);

    if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<AppointmentStatus>(status, true, out var parsedStatus))
        query = query.Where(a => a.Status == parsedStatus);

    if (from.HasValue)
        query = query.Where(a => a.ScheduledAt >= from.Value);

    if (to.HasValue)
        query = query.Where(a => a.ScheduledAt <= to.Value);

    if (!string.IsNullOrWhiteSpace(cursor))
    {
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            if (Guid.TryParse(decoded, out var lastId))
            {
                var lastItem = await db.Appointments.FindAsync(lastId);
                if (lastItem != null)
                    query = query.Where(a => a.CreatedAt < lastItem.CreatedAt ||
                                             (a.CreatedAt == lastItem.CreatedAt && a.Id.CompareTo(lastId) < 0));
            }
        }
        catch { /* ignore invalid cursor */ }
    }

    var items = await query.OrderByDescending(a => a.CreatedAt).Take(pageSize + 1).ToListAsync();
    var hasMore = items.Count > pageSize;
    if (hasMore) items.RemoveAt(pageSize);

    string? nextCursor = null;
    if (hasMore && items.Count > 0)
        nextCursor = Convert.ToBase64String(Encoding.UTF8.GetBytes(items[^1].Id.ToString()));

    return Results.Ok(new PaginatedResponse<AppointmentResponse>(
        items.Select(ToResponse).ToList(),
        nextCursor,
        hasMore));
});

// GET /api/v1/appointments/{id} — get detail
app.MapGet("/api/v1/appointments/{id:guid}", async (Guid id, AppointmentDbContext db) =>
{
    var appointment = await db.Appointments.FindAsync(id);
    return appointment is null ? Results.NotFound() : Results.Ok(ToResponse(appointment));
});

// Authorization: In production, CareCoordinator and Clinician roles can update appointment status.
// Audit: AppointmentCompleted/AppointmentMissed events record lifecycle transitions for compliance tracing.
app.MapMethods("/api/v1/appointments/{id:guid}", ["PATCH"],
    async (Guid id, UpdateAppointmentRequest request, AppointmentDbContext db, IEventPublisher publisher, ILogger<Program> logger) =>
    {
        var appointment = await db.Appointments.FindAsync(id);
        if (appointment is null) return Results.NotFound();

        if (appointment.Status is AppointmentStatus.Completed or AppointmentStatus.Canceled or AppointmentStatus.NoShow)
            return Results.Problem($"Cannot modify appointment in terminal status {appointment.Status}.", statusCode: 409, title: "Conflict");

        if (request.Status.HasValue)
        {
            var transitionError = ValidateTransition(appointment.Status, request.Status.Value);
            if (transitionError is not null)
                return Results.Problem(transitionError, statusCode: 409, title: "Conflict");

            appointment.Status = request.Status.Value;

            if (request.Status.Value == AppointmentStatus.Completed)
                appointment.CompletedAt = DateTimeOffset.UtcNow;
        }

        if (request.Notes is not null)
            appointment.Notes = request.Notes;

        appointment.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        // Publish appropriate events
        try
        {
            if (request.Status == AppointmentStatus.Booked)
            {
                await publisher.PublishAsync(new AppointmentBooked
                {
                    AppointmentId = appointment.Id,
                    CaseId = appointment.CaseId,
                    Type = appointment.Type.ToString(),
                    ScheduledAt = appointment.ScheduledAt
                });
            }
            else if (request.Status == AppointmentStatus.Completed)
            {
                await publisher.PublishAsync(new AppointmentCompleted
                {
                    AppointmentId = appointment.Id,
                    CaseId = appointment.CaseId,
                    CompletedAt = appointment.CompletedAt!.Value
                });
            }
            else if (request.Status == AppointmentStatus.NoShow)
            {
                await publisher.PublishAsync(new AppointmentMissed
                {
                    AppointmentId = appointment.Id,
                    CaseId = appointment.CaseId,
                    ScheduledAt = appointment.ScheduledAt
                });
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish appointment event for appointment {AppointmentId}.", appointment.Id);
        }

        return Results.Ok(ToResponse(appointment));
    });

app.Run();

static string? ValidateTransition(AppointmentStatus current, AppointmentStatus next)
{
    var valid = current switch
    {
        AppointmentStatus.Proposed => next is AppointmentStatus.Booked or AppointmentStatus.Canceled,
        AppointmentStatus.Booked => next is AppointmentStatus.Completed or AppointmentStatus.Canceled or AppointmentStatus.NoShow,
        _ => false
    };
    return valid ? null : $"Cannot transition from {current} to {next}.";
}

static AppointmentResponse ToResponse(Appointment appointment) => new(
    appointment.Id,
    appointment.CaseId,
    appointment.Type.ToString(),
    appointment.ScheduledAt,
    appointment.Status.ToString(),
    appointment.Notes,
    appointment.CreatedAt,
    appointment.UpdatedAt,
    appointment.CompletedAt,
    appointment.Status == AppointmentStatus.Booked && appointment.ScheduledAt < DateTimeOffset.UtcNow);

record CreateAppointmentRequest(
    Guid CaseId,
    AppointmentType? Type,
    DateTimeOffset ScheduledAt,
    AppointmentStatus? Status,
    string? Notes);

record UpdateAppointmentRequest(
    AppointmentStatus? Status,
    string? Notes);

record AppointmentResponse(
    Guid Id,
    Guid CaseId,
    string Type,
    DateTimeOffset ScheduledAt,
    string Status,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt,
    bool IsOverdue);

record PaginatedResponse<T>(List<T> Items, string? NextCursor, bool HasMore);
