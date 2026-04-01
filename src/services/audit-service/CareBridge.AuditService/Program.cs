using System.Text.Json;
using CareBridge.AuditService.Handlers;
using CareBridge.AuditService.Models;
using CareBridge.AuditService.Store;
using CareBridge.Shared.Contracts.Events;
using CareBridge.Shared.Contracts.Serialization;
using CareBridge.Shared.Infrastructure.Eventing;
using CareBridge.Shared.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.AddCareBridgeDefaults();

// Audit: In-memory store for local development. Cosmos DB in production.
// Append-only — IAuditStore has no Update or Delete methods by design.
builder.Services.AddSingleton<IAuditStore, InMemoryAuditStore>();

// Event handler — single handler processes all 14 domain event types
builder.Services.AddScoped<AuditEventHandler>();

builder.Services.AddScoped<IEventHandler<CaseCreated>>(sp => sp.GetRequiredService<AuditEventHandler>());
builder.Services.AddScoped<IEventHandler<CaseUpdated>>(sp => sp.GetRequiredService<AuditEventHandler>());
builder.Services.AddScoped<IEventHandler<CarePlanActivated>>(sp => sp.GetRequiredService<AuditEventHandler>());
builder.Services.AddScoped<IEventHandler<MilestoneCompleted>>(sp => sp.GetRequiredService<AuditEventHandler>());
builder.Services.AddScoped<IEventHandler<ObservationReceived>>(sp => sp.GetRequiredService<AuditEventHandler>());
builder.Services.AddScoped<IEventHandler<AlertRaised>>(sp => sp.GetRequiredService<AuditEventHandler>());
builder.Services.AddScoped<IEventHandler<AlertAcknowledged>>(sp => sp.GetRequiredService<AuditEventHandler>());
builder.Services.AddScoped<IEventHandler<AlertResolved>>(sp => sp.GetRequiredService<AuditEventHandler>());
builder.Services.AddScoped<IEventHandler<TaskCreated>>(sp => sp.GetRequiredService<AuditEventHandler>());
builder.Services.AddScoped<IEventHandler<TaskCompleted>>(sp => sp.GetRequiredService<AuditEventHandler>());
builder.Services.AddScoped<IEventHandler<AppointmentBooked>>(sp => sp.GetRequiredService<AuditEventHandler>());
builder.Services.AddScoped<IEventHandler<AppointmentCompleted>>(sp => sp.GetRequiredService<AuditEventHandler>());
builder.Services.AddScoped<IEventHandler<AppointmentMissed>>(sp => sp.GetRequiredService<AuditEventHandler>());
builder.Services.AddScoped<IEventHandler<NotificationSent>>(sp => sp.GetRequiredService<AuditEventHandler>());

// Event consumer — subscribe to all domain events with 5 retries (audit events are critical)
builder.Services.AddSingleton(new EventConsumerOptions
{
    QueueName = "audit-service",
    MaxRetries = 5,
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

// Audit: Read-only API. No POST, PUT, PATCH, or DELETE endpoints.
// HIPAA §164.312(b): Audit records are immutable — only created via event consumption.

// GET /api/v1/audit — query audit records with combinable filters
app.MapGet("/api/v1/audit", async (
    Guid? caseId,
    string? actorId,
    string? entityType,
    Guid? entityId,
    string? eventType,
    DateTimeOffset? from,
    DateTimeOffset? to,
    int? limit,
    string? cursor,
    IAuditStore store) =>
{
    var filter = new AuditQueryFilter
    {
        CaseId = caseId,
        ActorId = actorId,
        EntityType = entityType,
        EntityId = entityId,
        EventType = eventType,
        From = from,
        To = to,
        Limit = Math.Clamp(limit ?? 50, 1, 200),
        Cursor = cursor
    };

    var result = await store.QueryAsync(filter);

    return Results.Ok(new
    {
        Items = result.Items.Select(r => new
        {
            r.Id,
            r.Timestamp,
            r.CorrelationId,
            r.EventType,
            r.Action,
            r.ActorId,
            r.ActorRole,
            r.EntityType,
            r.EntityId,
            r.CaseId,
            r.ServiceSource
        }),
        result.NextCursor,
        result.HasMore,
        result.TotalCount
    });
});

// GET /api/v1/audit/{recordId} — single record with full payload as deserialized JSON
app.MapGet("/api/v1/audit/{recordId:guid}", async (Guid recordId, IAuditStore store) =>
{
    var record = await store.GetByIdAsync(recordId);
    if (record == null)
        return Results.NotFound();

    object? payloadObj = null;
    try
    {
        payloadObj = JsonSerializer.Deserialize<object>(record.Payload, JsonDefaults.Options);
    }
    catch
    {
        payloadObj = record.Payload;
    }

    return Results.Ok(new
    {
        record.Id,
        record.Timestamp,
        record.CorrelationId,
        record.EventType,
        record.Action,
        record.ActorId,
        record.ActorRole,
        record.EntityType,
        record.EntityId,
        record.CaseId,
        record.ServiceSource,
        Payload = payloadObj
    });
});

app.Run();
