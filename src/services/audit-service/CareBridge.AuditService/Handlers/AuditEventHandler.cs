using CareBridge.AuditService.Store;
using CareBridge.Shared.Contracts.Events;
using CareBridge.Shared.Infrastructure.Eventing;
using Microsoft.Extensions.Logging;

namespace CareBridge.AuditService.Handlers;

// Audit: Single handler for all domain events. Maps each event to an immutable audit record.
// Idempotency: Duplicate events (same EventId) are silently skipped.
public class AuditEventHandler :
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
    private readonly IAuditStore _store;
    private readonly ILogger<AuditEventHandler> _logger;

    public AuditEventHandler(IAuditStore store, ILogger<AuditEventHandler> logger)
    {
        _store = store;
        _logger = logger;
    }

    public Task HandleAsync(CaseCreated @event, CancellationToken ct = default) => ProcessAsync(@event);
    public Task HandleAsync(CaseUpdated @event, CancellationToken ct = default) => ProcessAsync(@event);
    public Task HandleAsync(CarePlanActivated @event, CancellationToken ct = default) => ProcessAsync(@event);
    public Task HandleAsync(MilestoneCompleted @event, CancellationToken ct = default) => ProcessAsync(@event);
    public Task HandleAsync(ObservationReceived @event, CancellationToken ct = default) => ProcessAsync(@event);
    public Task HandleAsync(AlertRaised @event, CancellationToken ct = default) => ProcessAsync(@event);
    public Task HandleAsync(AlertAcknowledged @event, CancellationToken ct = default) => ProcessAsync(@event);
    public Task HandleAsync(AlertResolved @event, CancellationToken ct = default) => ProcessAsync(@event);
    public Task HandleAsync(TaskCreated @event, CancellationToken ct = default) => ProcessAsync(@event);
    public Task HandleAsync(TaskCompleted @event, CancellationToken ct = default) => ProcessAsync(@event);
    public Task HandleAsync(AppointmentBooked @event, CancellationToken ct = default) => ProcessAsync(@event);
    public Task HandleAsync(AppointmentCompleted @event, CancellationToken ct = default) => ProcessAsync(@event);
    public Task HandleAsync(AppointmentMissed @event, CancellationToken ct = default) => ProcessAsync(@event);
    public Task HandleAsync(NotificationSent @event, CancellationToken ct = default) => ProcessAsync(@event);

    private async Task ProcessAsync(IntegrationEvent @event)
    {
        var existing = await _store.GetByEventIdAsync(@event.EventId);
        if (existing != null)
        {
            _logger.LogDebug("Duplicate event {EventId} skipped", @event.EventId);
            return;
        }

        var auditRecord = AuditEventMapper.Map(@event);
        await _store.AppendAsync(auditRecord);

        _logger.LogInformation("Audit record created for {EventType} (EventId: {EventId})",
            auditRecord.EventType, @event.EventId);
    }
}
