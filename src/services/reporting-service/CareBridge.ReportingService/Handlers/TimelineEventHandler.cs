using CareBridge.ReportingService.ReadModels;
using CareBridge.ReportingService.Store;
using CareBridge.Shared.Contracts.Events;
using CareBridge.Shared.Infrastructure.Eventing;

namespace CareBridge.ReportingService.Handlers;

public class TimelineEventHandler :
    IEventHandler<CaseCreated>,
    IEventHandler<CaseUpdated>,
    IEventHandler<CarePlanActivated>,
    IEventHandler<MilestoneCompleted>,
    IEventHandler<ObservationReceived>,
    IEventHandler<AlertRaised>,
    IEventHandler<AlertAcknowledged>,
    IEventHandler<AlertResolved>,
    IEventHandler<TaskCreated>,
    IEventHandler<TaskCompleted>,
    IEventHandler<AppointmentBooked>,
    IEventHandler<AppointmentCompleted>,
    IEventHandler<AppointmentMissed>,
    IEventHandler<NotificationSent>
{
    private readonly IReadModelStore _store;
    private readonly ILogger<TimelineEventHandler> _logger;

    public TimelineEventHandler(IReadModelStore store, ILogger<TimelineEventHandler> logger)
    {
        _store = store;
        _logger = logger;
    }

    public Task HandleAsync(CaseCreated @event, CancellationToken ct = default)
    {
        if (!TryDedup(@event)) return Task.CompletedTask;
        AddEntry(@event.CaseId, @event.OccurredAt, nameof(CaseCreated), "case",
            "Case created", $"Post-discharge case for {@event.DiagnosisDescription}",
            "system", entityId: @event.CaseId);
        _logger.LogInformation("[Reporting] Processed CaseCreated for case {CaseId}", @event.CaseId);
        return Task.CompletedTask;
    }

    public Task HandleAsync(CaseUpdated @event, CancellationToken ct = default)
    {
        if (!TryDedup(@event)) return Task.CompletedTask;
        AddEntry(@event.CaseId, @event.UpdatedAt, nameof(CaseUpdated), "case",
            "Case status changed", $"Status updated to {@event.Status}",
            "system", entityId: @event.CaseId);
        _logger.LogInformation("[Reporting] Processed CaseUpdated for case {CaseId}", @event.CaseId);
        return Task.CompletedTask;
    }

    public Task HandleAsync(CarePlanActivated @event, CancellationToken ct = default)
    {
        if (!TryDedup(@event)) return Task.CompletedTask;
        AddEntry(@event.CaseId, @event.ActivatedAt, nameof(CarePlanActivated), "careplan",
            "Care plan activated", $"{@event.TemplateName} with {@event.MilestoneCount} milestones",
            "system", entityId: @event.CarePlanId);
        _logger.LogInformation("[Reporting] Processed CarePlanActivated for case {CaseId}", @event.CaseId);
        return Task.CompletedTask;
    }

    public Task HandleAsync(MilestoneCompleted @event, CancellationToken ct = default)
    {
        if (!TryDedup(@event)) return Task.CompletedTask;
        AddEntry(@event.CaseId, @event.CompletedAt, nameof(MilestoneCompleted), "careplan",
            "Milestone completed", @event.MilestoneName,
            "system", entityId: @event.MilestoneId);
        _logger.LogInformation("[Reporting] Processed MilestoneCompleted for case {CaseId}", @event.CaseId);
        return Task.CompletedTask;
    }

    public Task HandleAsync(ObservationReceived @event, CancellationToken ct = default)
    {
        if (!TryDedup(@event)) return Task.CompletedTask;
        AddEntry(@event.CaseId, @event.RecordedAt, nameof(ObservationReceived), "observation",
            "Observation recorded", $"{@event.Type}: {@event.Value} {@event.Unit}",
            "system", entityId: @event.ObservationId);
        _logger.LogInformation("[Reporting] Processed ObservationReceived for case {CaseId}", @event.CaseId);
        return Task.CompletedTask;
    }

    public Task HandleAsync(AlertRaised @event, CancellationToken ct = default)
    {
        if (!TryDedup(@event)) return Task.CompletedTask;
        AddEntry(@event.CaseId, @event.CreatedAt, nameof(AlertRaised), "alert",
            $"Alert raised: {@event.Severity}", $"{@event.Title} — {@event.Description}",
            "system", severity: @event.Severity.ToString(), entityId: @event.AlertId);
        _logger.LogInformation("[Reporting] Processed AlertRaised for case {CaseId}", @event.CaseId);
        return Task.CompletedTask;
    }

    public Task HandleAsync(AlertAcknowledged @event, CancellationToken ct = default)
    {
        if (!TryDedup(@event)) return Task.CompletedTask;
        AddEntry(@event.CaseId, @event.AcknowledgedAt, nameof(AlertAcknowledged), "alert",
            "Alert acknowledged", $"Acknowledged by {@event.AcknowledgedBy}",
            @event.AcknowledgedBy, entityId: @event.AlertId);
        _logger.LogInformation("[Reporting] Processed AlertAcknowledged for case {CaseId}", @event.CaseId);
        return Task.CompletedTask;
    }

    public Task HandleAsync(AlertResolved @event, CancellationToken ct = default)
    {
        if (!TryDedup(@event)) return Task.CompletedTask;
        AddEntry(@event.CaseId, @event.ResolvedAt, nameof(AlertResolved), "alert",
            "Alert resolved", $"Resolved by {@event.ResolvedBy}",
            @event.ResolvedBy, entityId: @event.AlertId);
        _logger.LogInformation("[Reporting] Processed AlertResolved for case {CaseId}", @event.CaseId);
        return Task.CompletedTask;
    }

    public Task HandleAsync(TaskCreated @event, CancellationToken ct = default)
    {
        if (!TryDedup(@event)) return Task.CompletedTask;
        AddEntry(@event.CaseId, @event.CreatedAt, nameof(TaskCreated), "task",
            "Task created", $"{@event.Title} (Priority: {@event.Priority})",
            "system", entityId: @event.TaskId);
        _logger.LogInformation("[Reporting] Processed TaskCreated for case {CaseId}", @event.CaseId);
        return Task.CompletedTask;
    }

    public Task HandleAsync(TaskCompleted @event, CancellationToken ct = default)
    {
        if (!TryDedup(@event)) return Task.CompletedTask;
        AddEntry(@event.CaseId, @event.CompletedAt, nameof(TaskCompleted), "task",
            "Task completed", $"Completed by {@event.CompletedBy}",
            @event.CompletedBy, entityId: @event.TaskId);
        _logger.LogInformation("[Reporting] Processed TaskCompleted for case {CaseId}", @event.CaseId);
        return Task.CompletedTask;
    }

    public Task HandleAsync(AppointmentBooked @event, CancellationToken ct = default)
    {
        if (!TryDedup(@event)) return Task.CompletedTask;
        AddEntry(@event.CaseId, @event.OccurredAt, nameof(AppointmentBooked), "appointment",
            "Appointment booked", $"{@event.Type} scheduled for {FormatDate(@event.ScheduledAt)}",
            "system", entityId: @event.AppointmentId);
        _logger.LogInformation("[Reporting] Processed AppointmentBooked for case {CaseId}", @event.CaseId);
        return Task.CompletedTask;
    }

    public Task HandleAsync(AppointmentCompleted @event, CancellationToken ct = default)
    {
        if (!TryDedup(@event)) return Task.CompletedTask;
        AddEntry(@event.CaseId, @event.CompletedAt, nameof(AppointmentCompleted), "appointment",
            "Appointment completed", "Appointment completed",
            "system", entityId: @event.AppointmentId);
        _logger.LogInformation("[Reporting] Processed AppointmentCompleted for case {CaseId}", @event.CaseId);
        return Task.CompletedTask;
    }

    public Task HandleAsync(AppointmentMissed @event, CancellationToken ct = default)
    {
        if (!TryDedup(@event)) return Task.CompletedTask;
        AddEntry(@event.CaseId, @event.OccurredAt, nameof(AppointmentMissed), "appointment",
            "Appointment missed", "Appointment was missed (no-show)",
            "system", entityId: @event.AppointmentId);
        _logger.LogInformation("[Reporting] Processed AppointmentMissed for case {CaseId}", @event.CaseId);
        return Task.CompletedTask;
    }

    public Task HandleAsync(NotificationSent @event, CancellationToken ct = default)
    {
        if (!TryDedup(@event)) return Task.CompletedTask;
        AddEntry(@event.CaseId, @event.SentAt, nameof(NotificationSent), "notification",
            "Notification sent", $"{@event.Channel}: {@event.Subject}",
            "system", entityId: @event.NotificationId);
        _logger.LogInformation("[Reporting] Processed NotificationSent for case {CaseId}", @event.CaseId);
        return Task.CompletedTask;
    }

    private bool TryDedup(IntegrationEvent @event)
    {
        if (_store.HasProcessedEvent(@event.EventId))
        {
            _logger.LogDebug("[Reporting] Skipping duplicate timeline event {EventId}", @event.EventId);
            return false;
        }
        _store.MarkEventProcessed(@event.EventId);
        return true;
    }

    private void AddEntry(Guid caseId, DateTimeOffset timestamp, string eventType, string category,
        string title, string description, string actor, string? severity = null, Guid? entityId = null)
    {
        _store.AddTimelineEntry(caseId, new TimelineEntry
        {
            Id = Guid.NewGuid(),
            Timestamp = timestamp,
            EventType = eventType,
            Category = category,
            Title = title,
            Description = description,
            Actor = actor,
            Severity = severity,
            EntityId = entityId
        });
    }

    private static string FormatDate(DateTimeOffset dt) => dt.ToString("MMM dd, yyyy HH:mm");
}
