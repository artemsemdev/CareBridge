using System.Text;
using CareBridge.NotificationService.Data;
using CareBridge.NotificationService.Handlers;
using CareBridge.Shared.Contracts.Events;
using CareBridge.Shared.Infrastructure.Eventing;
using CareBridge.Shared.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddCareBridgeDefaults();

builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("NotificationDb")));

builder.Services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();

builder.Services.AddScoped<AlertRaisedNotificationHandler>();
builder.Services.AddScoped<AppointmentBookedNotificationHandler>();
builder.Services.AddScoped<AppointmentMissedNotificationHandler>();
builder.Services.AddScoped<MilestoneCompletedNotificationHandler>();

builder.Services.AddSingleton(new EventConsumerOptions
{
    QueueName = "notification-service",
    RoutingKeys = [nameof(AlertRaised), nameof(AppointmentBooked), nameof(AppointmentMissed), nameof(MilestoneCompleted)],
    EventTypeMap = new Dictionary<string, Type>
    {
        [nameof(AlertRaised)] = typeof(AlertRaised),
        [nameof(AppointmentBooked)] = typeof(AppointmentBooked),
        [nameof(AppointmentMissed)] = typeof(AppointmentMissed),
        [nameof(MilestoneCompleted)] = typeof(MilestoneCompleted)
    }
});
builder.Services.AddScoped<IEventHandler<AlertRaised>, AlertRaisedNotificationHandler>();
builder.Services.AddScoped<IEventHandler<AppointmentBooked>, AppointmentBookedNotificationHandler>();
builder.Services.AddScoped<IEventHandler<AppointmentMissed>, AppointmentMissedNotificationHandler>();
builder.Services.AddScoped<IEventHandler<MilestoneCompleted>, MilestoneCompletedNotificationHandler>();
builder.Services.AddHostedService<EventConsumerBackgroundService>();

var app = builder.Build();

app.UseCareBridgeDefaults();
app.MapCareBridgeHealthChecks();

// GET /api/v1/notifications — notification history with filters
app.MapGet("/api/v1/notifications", async (
    NotificationDbContext db,
    Guid? caseId,
    int? limit,
    string? cursor) =>
{
    var pageSize = Math.Min(limit ?? 20, 100);
    var query = db.Notifications.AsQueryable();

    if (caseId.HasValue)
        query = query.Where(n => n.CaseId == caseId.Value);

    if (!string.IsNullOrWhiteSpace(cursor))
    {
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            if (Guid.TryParse(decoded, out var lastId))
            {
                var lastItem = await db.Notifications.FindAsync(lastId);
                if (lastItem != null)
                    query = query.Where(n => n.CreatedAt < lastItem.CreatedAt ||
                                             (n.CreatedAt == lastItem.CreatedAt && n.Id.CompareTo(lastId) < 0));
            }
        }
        catch { /* ignore invalid cursor */ }
    }

    var items = await query.OrderByDescending(n => n.CreatedAt).Take(pageSize + 1).ToListAsync();
    var hasMore = items.Count > pageSize;
    if (hasMore) items.RemoveAt(pageSize);

    string? nextCursor = null;
    if (hasMore && items.Count > 0)
        nextCursor = Convert.ToBase64String(Encoding.UTF8.GetBytes(items[^1].Id.ToString()));

    return Results.Ok(new PaginatedResponse<NotificationResponse>(
        items.Select(n => new NotificationResponse(
            n.Id, n.CaseId, n.Channel.ToString(), n.Recipient,
            n.Subject, n.Body, n.Status.ToString(), n.TriggeredBy, n.CreatedAt)).ToList(),
        nextCursor,
        hasMore));
});

app.Run();

record NotificationResponse(
    Guid Id,
    Guid CaseId,
    string Channel,
    string Recipient,
    string Subject,
    string Body,
    string Status,
    string TriggeredBy,
    DateTimeOffset CreatedAt);

record PaginatedResponse<T>(List<T> Items, string? NextCursor, bool HasMore);
