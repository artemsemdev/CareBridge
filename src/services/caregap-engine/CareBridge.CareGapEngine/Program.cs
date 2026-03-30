using System.Text;
using CareBridge.CareGapEngine.Data;
using CareBridge.CareGapEngine.Handlers;
using CareBridge.CareGapEngine.Services;
using CareBridge.Shared.Contracts.Enums;
using CareBridge.Shared.Contracts.Events;
using CareBridge.Shared.Infrastructure.Eventing;
using CareBridge.Shared.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddCareBridgeDefaults();

builder.Services.AddDbContext<CareGapDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("CareGapDb")));

builder.Services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();
builder.Services.AddScoped<ObservationReceivedHandler>();

builder.Services.AddSingleton(new EventConsumerOptions
{
    QueueName = "caregap-engine",
    RoutingKeys = [nameof(ObservationReceived)],
    EventTypeMap = new Dictionary<string, Type>
    {
        [nameof(ObservationReceived)] = typeof(ObservationReceived)
    }
});
builder.Services.AddScoped<IEventHandler<ObservationReceived>, ObservationReceivedHandler>();
builder.Services.AddHostedService<EventConsumerBackgroundService>();

var carePlanServiceUrl = builder.Configuration["Services:CarePlanService"] ?? "http://localhost:5020";
builder.Services.AddHttpClient("CarePlanService", client =>
{
    client.BaseAddress = new Uri(carePlanServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(15);
});

builder.Services.AddHostedService<MilestoneScanBackgroundService>();

var app = builder.Build();

app.UseCareBridgeDefaults();
app.MapCareBridgeHealthChecks();

// GET /api/v1/alerts
app.MapGet("/api/v1/alerts", async (
    CareGapDbContext db,
    Guid? caseId,
    string? status,
    string? severity,
    string? type,
    int? limit,
    string? cursor) =>
{
    var pageSize = Math.Min(limit ?? 20, 100);
    var query = db.Alerts.AsQueryable();

    if (caseId.HasValue)
        query = query.Where(a => a.CaseId == caseId.Value);

    if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<AlertStatus>(status, true, out var parsedStatus))
        query = query.Where(a => a.Status == parsedStatus);

    if (!string.IsNullOrWhiteSpace(severity) && Enum.TryParse<Severity>(severity, true, out var parsedSeverity))
        query = query.Where(a => a.Severity == parsedSeverity);

    if (!string.IsNullOrWhiteSpace(type) && Enum.TryParse<AlertType>(type, true, out var parsedType))
        query = query.Where(a => a.Type == parsedType);

    if (!string.IsNullOrWhiteSpace(cursor))
    {
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            if (Guid.TryParse(decoded, out var lastId))
            {
                var lastItem = await db.Alerts.FindAsync(lastId);
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

    return Results.Ok(new PaginatedResponse<AlertResponse>(
        items.Select(ToResponse).ToList(),
        nextCursor,
        hasMore));
});

// GET /api/v1/alerts/{alertId}
app.MapGet("/api/v1/alerts/{alertId:guid}", async (Guid alertId, CareGapDbContext db) =>
{
    var alert = await db.Alerts.FindAsync(alertId);
    return alert is null ? Results.NotFound() : Results.Ok(ToResponse(alert));
});

// PATCH /api/v1/alerts/{alertId}/acknowledge
app.MapMethods("/api/v1/alerts/{alertId:guid}/acknowledge", ["PATCH"],
    async (Guid alertId, AlertActionRequest request, CareGapDbContext db, IEventPublisher publisher, ILogger<Program> logger) =>
    {
        var alert = await db.Alerts.FindAsync(alertId);
        if (alert is null) return Results.NotFound();

        if (alert.Status != AlertStatus.Open)
            return Results.Problem($"Cannot acknowledge alert in status {alert.Status}.", statusCode: 409, title: "Conflict");

        var actor = request.Actor ?? "care-coordinator";
        var now = DateTimeOffset.UtcNow;
        alert.Status = AlertStatus.Acknowledged;
        alert.AcknowledgedAt = now;
        alert.AcknowledgedBy = actor;
        await db.SaveChangesAsync();

        try
        {
            await publisher.PublishAsync(new AlertAcknowledged
            {
                AlertId = alert.Id,
                CaseId = alert.CaseId,
                AcknowledgedBy = actor,
                AcknowledgedAt = now
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish AlertAcknowledged event for alert {AlertId}.", alert.Id);
        }

        return Results.Ok(ToResponse(alert));
    });

// PATCH /api/v1/alerts/{alertId}/resolve
app.MapMethods("/api/v1/alerts/{alertId:guid}/resolve", ["PATCH"],
    async (Guid alertId, AlertActionRequest request, CareGapDbContext db, IEventPublisher publisher, ILogger<Program> logger) =>
    {
        var alert = await db.Alerts.FindAsync(alertId);
        if (alert is null) return Results.NotFound();

        if (alert.Status == AlertStatus.Resolved)
            return Results.Problem("Alert is already resolved.", statusCode: 409, title: "Conflict");

        if (alert.Status != AlertStatus.Acknowledged)
            return Results.Problem("Alert must be acknowledged before it can be resolved.", statusCode: 409, title: "Conflict");

        var actor = request.Actor ?? "care-coordinator";
        var now = DateTimeOffset.UtcNow;
        alert.Status = AlertStatus.Resolved;
        alert.ResolvedAt = now;
        alert.ResolvedBy = actor;
        await db.SaveChangesAsync();

        try
        {
            await publisher.PublishAsync(new AlertResolved
            {
                AlertId = alert.Id,
                CaseId = alert.CaseId,
                ResolvedBy = actor,
                ResolvedAt = now
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish AlertResolved event for alert {AlertId}.", alert.Id);
        }

        return Results.Ok(ToResponse(alert));
    });

app.Run();

static AlertResponse ToResponse(CareBridge.CareGapEngine.Entities.Alert alert)
{
    var age = FormatAge(alert.CreatedAt);
    return new AlertResponse(
        alert.Id,
        alert.CaseId,
        alert.Type.ToString(),
        alert.Severity.ToString(),
        alert.Title,
        alert.Description,
        alert.SourceEventId,
        alert.Status.ToString(),
        alert.CreatedAt,
        alert.AcknowledgedAt,
        alert.AcknowledgedBy,
        alert.ResolvedAt,
        alert.ResolvedBy,
        age);
}

static string FormatAge(DateTimeOffset createdAt)
{
    var elapsed = DateTimeOffset.UtcNow - createdAt;
    if (elapsed.TotalMinutes < 1) return "just now";
    if (elapsed.TotalMinutes < 60) return $"{(int)elapsed.TotalMinutes} minute{((int)elapsed.TotalMinutes != 1 ? "s" : "")} ago";
    if (elapsed.TotalHours < 24) return $"{(int)elapsed.TotalHours} hour{((int)elapsed.TotalHours != 1 ? "s" : "")} ago";
    return $"{(int)elapsed.TotalDays} day{((int)elapsed.TotalDays != 1 ? "s" : "")} ago";
}

record AlertActionRequest(string? Actor);

record AlertResponse(
    Guid Id,
    Guid CaseId,
    string Type,
    string Severity,
    string Title,
    string Description,
    Guid? SourceEventId,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? AcknowledgedAt,
    string? AcknowledgedBy,
    DateTimeOffset? ResolvedAt,
    string? ResolvedBy,
    string Age);

record PaginatedResponse<T>(List<T> Items, string? NextCursor, bool HasMore);
