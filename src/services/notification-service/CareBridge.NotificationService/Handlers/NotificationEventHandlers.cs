using CareBridge.NotificationService.Data;
using CareBridge.NotificationService.Entities;
using CareBridge.Shared.Contracts.Enums;
using CareBridge.Shared.Contracts.Events;
using CareBridge.Shared.Infrastructure.Eventing;

namespace CareBridge.NotificationService.Handlers;

public class AlertRaisedNotificationHandler : IEventHandler<AlertRaised>
{
    private readonly NotificationDbContext _db;
    private readonly IEventPublisher _publisher;
    private readonly ILogger<AlertRaisedNotificationHandler> _logger;

    public AlertRaisedNotificationHandler(NotificationDbContext db, IEventPublisher publisher, ILogger<AlertRaisedNotificationHandler> logger)
    {
        _db = db;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task HandleAsync(AlertRaised @event, CancellationToken ct = default)
    {
        var channels = @event.Severity is Severity.Critical or Severity.High
            ? new[] { NotificationChannel.Email, NotificationChannel.InApp }
            : @event.Severity == Severity.Medium
                ? new[] { NotificationChannel.InApp }
                : Array.Empty<NotificationChannel>();

        if (channels.Length == 0) return;

        var subjectPrefix = @event.Severity is Severity.Critical or Severity.High ? "Alert" : "Notice";
        var subject = $"{subjectPrefix}: {SanitizeTitle(@event.Title)} — Case {FormatId(@event.CaseId)}";
        var body = $"An alert has been raised for case {FormatId(@event.CaseId)}.\n\nSeverity: {@event.Severity}\nType: {@event.AlertType}\nTitle: {@event.Title}\nDescription: {@event.Description}\nTime: {@event.CreatedAt:u}";

        foreach (var channel in channels)
        {
            var recipient = channel == NotificationChannel.Email
                ? "coordinator@carebridge.local"
                : "care-coordinator";

            await CreateAndLogNotification(@event.CaseId, channel, recipient, subject, body, nameof(AlertRaised), ct);
        }
    }

    private async Task CreateAndLogNotification(Guid caseId, NotificationChannel channel, string recipient, string subject, string body, string triggeredBy, CancellationToken ct)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            CaseId = caseId,
            Channel = channel,
            Recipient = recipient,
            Subject = subject,
            Body = body,
            Status = NotificationStatus.Sent,
            TriggeredBy = triggeredBy,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("[Notification] Channel={Channel}, To={Recipient}, Subject=\"{Subject}\", Body=\"{Body}\"",
            channel, recipient, subject, body);

        try
        {
            await _publisher.PublishAsync(new NotificationSent
            {
                NotificationId = notification.Id,
                CaseId = notification.CaseId,
                Channel = notification.Channel,
                Recipient = notification.Recipient,
                Subject = notification.Subject,
                SentAt = notification.CreatedAt
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish NotificationSent event for notification {NotificationId}.", notification.Id);
        }
    }

    private static string SanitizeTitle(string title) => title.Length > 100 ? title[..100] + "..." : title;
    private static string FormatId(Guid id) => id.ToString()[..8];
}

public class AppointmentBookedNotificationHandler : IEventHandler<AppointmentBooked>
{
    private readonly NotificationDbContext _db;
    private readonly IEventPublisher _publisher;
    private readonly ILogger<AppointmentBookedNotificationHandler> _logger;

    public AppointmentBookedNotificationHandler(NotificationDbContext db, IEventPublisher publisher, ILogger<AppointmentBookedNotificationHandler> logger)
    {
        _db = db;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task HandleAsync(AppointmentBooked @event, CancellationToken ct = default)
    {
        var subject = $"Appointment scheduled — Case {FormatId(@event.CaseId)}";
        var body = $"An appointment has been scheduled for case {FormatId(@event.CaseId)}.\n\nType: {@event.Type}\nScheduled: {@event.ScheduledAt:u}";

        await CreateAndLogNotification(@event.CaseId, NotificationChannel.InApp, "care-coordinator", subject, body, nameof(AppointmentBooked), ct);
    }

    private async Task CreateAndLogNotification(Guid caseId, NotificationChannel channel, string recipient, string subject, string body, string triggeredBy, CancellationToken ct)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            CaseId = caseId,
            Channel = channel,
            Recipient = recipient,
            Subject = subject,
            Body = body,
            Status = NotificationStatus.Sent,
            TriggeredBy = triggeredBy,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("[Notification] Channel={Channel}, To={Recipient}, Subject=\"{Subject}\", Body=\"{Body}\"",
            channel, recipient, subject, body);

        try
        {
            await _publisher.PublishAsync(new NotificationSent
            {
                NotificationId = notification.Id,
                CaseId = notification.CaseId,
                Channel = notification.Channel,
                Recipient = notification.Recipient,
                Subject = notification.Subject,
                SentAt = notification.CreatedAt
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish NotificationSent event for notification {NotificationId}.", notification.Id);
        }
    }

    private static string FormatId(Guid id) => id.ToString()[..8];
}

public class AppointmentMissedNotificationHandler : IEventHandler<AppointmentMissed>
{
    private readonly NotificationDbContext _db;
    private readonly IEventPublisher _publisher;
    private readonly ILogger<AppointmentMissedNotificationHandler> _logger;

    public AppointmentMissedNotificationHandler(NotificationDbContext db, IEventPublisher publisher, ILogger<AppointmentMissedNotificationHandler> logger)
    {
        _db = db;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task HandleAsync(AppointmentMissed @event, CancellationToken ct = default)
    {
        var subject = $"Missed appointment — Case {FormatId(@event.CaseId)}";
        var body = $"A scheduled appointment was missed for case {FormatId(@event.CaseId)}.\n\nScheduled: {@event.ScheduledAt:u}\nPlease follow up with the patient.";

        foreach (var channel in new[] { NotificationChannel.Email, NotificationChannel.InApp })
        {
            var recipient = channel == NotificationChannel.Email
                ? "coordinator@carebridge.local"
                : "care-coordinator";

            await CreateAndLogNotification(@event.CaseId, channel, recipient, subject, body, nameof(AppointmentMissed), ct);
        }
    }

    private async Task CreateAndLogNotification(Guid caseId, NotificationChannel channel, string recipient, string subject, string body, string triggeredBy, CancellationToken ct)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            CaseId = caseId,
            Channel = channel,
            Recipient = recipient,
            Subject = subject,
            Body = body,
            Status = NotificationStatus.Sent,
            TriggeredBy = triggeredBy,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("[Notification] Channel={Channel}, To={Recipient}, Subject=\"{Subject}\", Body=\"{Body}\"",
            channel, recipient, subject, body);

        try
        {
            await _publisher.PublishAsync(new NotificationSent
            {
                NotificationId = notification.Id,
                CaseId = notification.CaseId,
                Channel = notification.Channel,
                Recipient = notification.Recipient,
                Subject = notification.Subject,
                SentAt = notification.CreatedAt
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish NotificationSent event for notification {NotificationId}.", notification.Id);
        }
    }

    private static string FormatId(Guid id) => id.ToString()[..8];
}

public class MilestoneCompletedNotificationHandler : IEventHandler<MilestoneCompleted>
{
    private readonly NotificationDbContext _db;
    private readonly IEventPublisher _publisher;
    private readonly ILogger<MilestoneCompletedNotificationHandler> _logger;

    public MilestoneCompletedNotificationHandler(NotificationDbContext db, IEventPublisher publisher, ILogger<MilestoneCompletedNotificationHandler> logger)
    {
        _db = db;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task HandleAsync(MilestoneCompleted @event, CancellationToken ct = default)
    {
        var subject = $"Milestone completed: {@event.MilestoneName}";
        var body = $"A care plan milestone has been completed for case {FormatId(@event.CaseId)}.\n\nMilestone: {@event.MilestoneName}\nCompleted: {@event.CompletedAt:u}";

        await CreateAndLogNotification(@event.CaseId, NotificationChannel.InApp, "care-coordinator", subject, body, nameof(MilestoneCompleted), ct);
    }

    private async Task CreateAndLogNotification(Guid caseId, NotificationChannel channel, string recipient, string subject, string body, string triggeredBy, CancellationToken ct)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            CaseId = caseId,
            Channel = channel,
            Recipient = recipient,
            Subject = subject,
            Body = body,
            Status = NotificationStatus.Sent,
            TriggeredBy = triggeredBy,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("[Notification] Channel={Channel}, To={Recipient}, Subject=\"{Subject}\", Body=\"{Body}\"",
            channel, recipient, subject, body);

        try
        {
            await _publisher.PublishAsync(new NotificationSent
            {
                NotificationId = notification.Id,
                CaseId = notification.CaseId,
                Channel = notification.Channel,
                Recipient = notification.Recipient,
                Subject = notification.Subject,
                SentAt = notification.CreatedAt
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish NotificationSent event for notification {NotificationId}.", notification.Id);
        }
    }

    private static string FormatId(Guid id) => id.ToString()[..8];
}
