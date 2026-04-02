using CareBridge.ReportingService.Handlers;
using CareBridge.ReportingService.Store;
using CareBridge.Shared.Contracts.Events;
using CareBridge.Shared.Infrastructure.Eventing;
using CareBridge.Shared.Infrastructure.Extensions;
using CareBridge.Shared.Infrastructure.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.AddCareBridgeDefaults();

var rabbitHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
var rabbitPort = int.Parse(builder.Configuration["RabbitMQ:Port"] ?? "5672");
var rabbitUser = builder.Configuration["RabbitMQ:User"] ?? "guest";
var rabbitPassword = builder.Configuration["RabbitMQ:Password"] ?? "guest";
builder.Services.AddHealthChecks()
    .AddCareBridgeRabbitMQ(rabbitHost, rabbitPort, rabbitUser, rabbitPassword);

// HIPAA Minimum Necessary: Reporting Service stores aggregate counts and summary data only.
// Individual patient records are referenced by ID — no patient names stored in dashboard metrics.
// Read model store — in-memory for local dev, swappable via DI
builder.Services.AddSingleton<IReadModelStore, InMemoryReadModelStore>();

// Event handler registrations
builder.Services.AddScoped<DashboardEventHandler>();
builder.Services.AddScoped<TimelineEventHandler>();
builder.Services.AddScoped<ReportingEventDispatcher>();

builder.Services.AddScoped<IEventHandler<CaseCreated>>(sp => sp.GetRequiredService<ReportingEventDispatcher>());
builder.Services.AddScoped<IEventHandler<CaseUpdated>>(sp => sp.GetRequiredService<ReportingEventDispatcher>());
builder.Services.AddScoped<IEventHandler<CarePlanActivated>>(sp => sp.GetRequiredService<ReportingEventDispatcher>());
builder.Services.AddScoped<IEventHandler<MilestoneCompleted>>(sp => sp.GetRequiredService<ReportingEventDispatcher>());
builder.Services.AddScoped<IEventHandler<ObservationReceived>>(sp => sp.GetRequiredService<ReportingEventDispatcher>());
builder.Services.AddScoped<IEventHandler<AlertRaised>>(sp => sp.GetRequiredService<ReportingEventDispatcher>());
builder.Services.AddScoped<IEventHandler<AlertAcknowledged>>(sp => sp.GetRequiredService<ReportingEventDispatcher>());
builder.Services.AddScoped<IEventHandler<AlertResolved>>(sp => sp.GetRequiredService<ReportingEventDispatcher>());
builder.Services.AddScoped<IEventHandler<TaskCreated>>(sp => sp.GetRequiredService<ReportingEventDispatcher>());
builder.Services.AddScoped<IEventHandler<TaskCompleted>>(sp => sp.GetRequiredService<ReportingEventDispatcher>());
builder.Services.AddScoped<IEventHandler<AppointmentBooked>>(sp => sp.GetRequiredService<ReportingEventDispatcher>());
builder.Services.AddScoped<IEventHandler<AppointmentCompleted>>(sp => sp.GetRequiredService<ReportingEventDispatcher>());
builder.Services.AddScoped<IEventHandler<AppointmentMissed>>(sp => sp.GetRequiredService<ReportingEventDispatcher>());
builder.Services.AddScoped<IEventHandler<NotificationSent>>(sp => sp.GetRequiredService<ReportingEventDispatcher>());

// Event consumer — subscribe to all domain events
builder.Services.AddSingleton(new EventConsumerOptions
{
    QueueName = "reporting-service",
    RoutingKeys =
    [
        nameof(CaseCreated), nameof(CaseUpdated),
        nameof(CarePlanActivated), nameof(MilestoneCompleted),
        nameof(ObservationReceived),
        nameof(AlertRaised), nameof(AlertAcknowledged), nameof(AlertResolved),
        nameof(TaskCreated), nameof(TaskCompleted),
        nameof(AppointmentBooked), nameof(AppointmentCompleted), nameof(AppointmentMissed),
        nameof(NotificationSent)
    ],
    EventTypeMap = new Dictionary<string, Type>
    {
        [nameof(CaseCreated)] = typeof(CaseCreated),
        [nameof(CaseUpdated)] = typeof(CaseUpdated),
        [nameof(CarePlanActivated)] = typeof(CarePlanActivated),
        [nameof(MilestoneCompleted)] = typeof(MilestoneCompleted),
        [nameof(ObservationReceived)] = typeof(ObservationReceived),
        [nameof(AlertRaised)] = typeof(AlertRaised),
        [nameof(AlertAcknowledged)] = typeof(AlertAcknowledged),
        [nameof(AlertResolved)] = typeof(AlertResolved),
        [nameof(TaskCreated)] = typeof(TaskCreated),
        [nameof(TaskCompleted)] = typeof(TaskCompleted),
        [nameof(AppointmentBooked)] = typeof(AppointmentBooked),
        [nameof(AppointmentCompleted)] = typeof(AppointmentCompleted),
        [nameof(AppointmentMissed)] = typeof(AppointmentMissed),
        [nameof(NotificationSent)] = typeof(NotificationSent)
    }
});
builder.Services.AddHostedService<EventConsumerBackgroundService>();

var app = builder.Build();

app.UseCareBridgeDefaults();
app.MapCareBridgeHealthChecks();

// HIPAA Minimum Necessary: Dashboard returns aggregate counts and recent case summaries.
// Patient names appear in RecentCases for coordinator workflow — no contact info or clinical values.
// Authorization: In production, all authenticated roles can view the dashboard (read-only).

app.MapGet("/api/v1/reports/dashboard", (IReadModelStore store) =>
{
    var summary = store.GetDashboardSummary();
    return Results.Ok(new
    {
        summary.ActiveCaseCount,
        Alerts = new
        {
            summary.AlertsByStatus.Open,
            summary.AlertsByStatus.Acknowledged,
            BySeverity = new
            {
                summary.AlertsBySeverity.Critical,
                summary.AlertsBySeverity.High,
                summary.AlertsBySeverity.Medium,
                summary.AlertsBySeverity.Informational
            }
        },
        Tasks = new
        {
            Open = summary.OpenTaskCount,
            Overdue = summary.OverdueTaskCount
        },
        Appointments = new
        {
            Pending = summary.PendingAppointmentCount
        },
        RecentCases = summary.RecentCases.Select(c => new
        {
            c.Id,
            c.PatientName,
            c.Status,
            c.DischargeDate,
            c.CreatedAt
        }),
        TopAlerts = summary.TopAlerts.Select(a => new
        {
            a.Id,
            a.CaseId,
            a.Title,
            a.Severity,
            Age = FormatAge(a.CreatedAt),
            a.CreatedAt
        }),
        summary.LastUpdatedAt
    });
});

// PHI: Timeline entries may contain clinical event descriptions (alert details, observation types).
// Authorization: In production, CareCoordinator and Clinician roles can view case timelines.
// HIPAA Minimum Necessary: Timeline entries reference entities by ID — no patient demographics included.

app.MapGet("/api/v1/reports/timeline/{caseId:guid}", (Guid caseId, int? limit, string? cursor, string? category, IReadModelStore store) =>
{
    var pageSize = Math.Clamp(limit ?? 50, 1, 200);
    var timeline = store.GetOrCreateTimeline(caseId);

    // Entries are already sorted newest-first by the store
    var entries = timeline.Entries.AsEnumerable();

    // Apply category filter
    if (!string.IsNullOrEmpty(category))
        entries = entries.Where(e => e.Category.Equals(category, StringComparison.OrdinalIgnoreCase));

    var allFiltered = entries.ToList();
    var totalCount = allFiltered.Count;

    // Apply cursor-based pagination (cursor = base64-encoded entry ID)
    if (!string.IsNullOrEmpty(cursor))
    {
        var cursorBytes = Convert.FromBase64String(cursor);
        var cursorId = new Guid(cursorBytes);
        var idx = allFiltered.FindIndex(e => e.Id == cursorId);
        if (idx >= 0)
            allFiltered = allFiltered.Skip(idx + 1).ToList();
    }

    var page = allFiltered.Take(pageSize + 1).ToList();
    var hasMore = page.Count > pageSize;
    if (hasMore) page.RemoveAt(page.Count - 1);

    string? nextCursor = null;
    if (hasMore && page.Count > 0)
        nextCursor = Convert.ToBase64String(page[^1].Id.ToByteArray());

    return Results.Ok(new
    {
        CaseId = caseId,
        Entries = page.Select(e => new
        {
            e.Id,
            e.Timestamp,
            e.EventType,
            e.Category,
            e.Title,
            e.Description,
            e.Actor,
            e.Severity,
            e.EntityId
        }),
        NextCursor = nextCursor,
        HasMore = hasMore,
        TotalCount = totalCount
    });
});

app.Run();

static string FormatAge(DateTimeOffset createdAt)
{
    var span = DateTimeOffset.UtcNow - createdAt;
    if (span.TotalMinutes < 1) return "just now";
    if (span.TotalHours < 1) return $"{(int)span.TotalMinutes}m ago";
    if (span.TotalDays < 1) return $"{(int)span.TotalHours}h ago";
    return $"{(int)span.TotalDays}d ago";
}
