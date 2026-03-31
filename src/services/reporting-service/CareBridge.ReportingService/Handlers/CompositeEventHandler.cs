using CareBridge.Shared.Contracts.Events;
using CareBridge.Shared.Infrastructure.Eventing;

namespace CareBridge.ReportingService.Handlers;

public class ReportingEventDispatcher :
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
    private readonly DashboardEventHandler _dashboard;
    private readonly TimelineEventHandler _timeline;

    public ReportingEventDispatcher(DashboardEventHandler dashboard, TimelineEventHandler timeline)
    {
        _dashboard = dashboard;
        _timeline = timeline;
    }

    public async Task HandleAsync(CaseCreated @event, CancellationToken ct = default)
    {
        await _dashboard.HandleAsync(@event, ct);
        await _timeline.HandleAsync(@event, ct);
    }

    public async Task HandleAsync(CaseUpdated @event, CancellationToken ct = default)
    {
        await _dashboard.HandleAsync(@event, ct);
        await _timeline.HandleAsync(@event, ct);
    }

    public Task HandleAsync(CarePlanActivated @event, CancellationToken ct = default)
        => _timeline.HandleAsync(@event, ct);

    public Task HandleAsync(MilestoneCompleted @event, CancellationToken ct = default)
        => _timeline.HandleAsync(@event, ct);

    public Task HandleAsync(ObservationReceived @event, CancellationToken ct = default)
        => _timeline.HandleAsync(@event, ct);

    public async Task HandleAsync(AlertRaised @event, CancellationToken ct = default)
    {
        await _dashboard.HandleAsync(@event, ct);
        await _timeline.HandleAsync(@event, ct);
    }

    public async Task HandleAsync(AlertAcknowledged @event, CancellationToken ct = default)
    {
        await _dashboard.HandleAsync(@event, ct);
        await _timeline.HandleAsync(@event, ct);
    }

    public async Task HandleAsync(AlertResolved @event, CancellationToken ct = default)
    {
        await _dashboard.HandleAsync(@event, ct);
        await _timeline.HandleAsync(@event, ct);
    }

    public async Task HandleAsync(TaskCreated @event, CancellationToken ct = default)
    {
        await _dashboard.HandleAsync(@event, ct);
        await _timeline.HandleAsync(@event, ct);
    }

    public async Task HandleAsync(TaskCompleted @event, CancellationToken ct = default)
    {
        await _dashboard.HandleAsync(@event, ct);
        await _timeline.HandleAsync(@event, ct);
    }

    public async Task HandleAsync(AppointmentBooked @event, CancellationToken ct = default)
    {
        await _dashboard.HandleAsync(@event, ct);
        await _timeline.HandleAsync(@event, ct);
    }

    public async Task HandleAsync(AppointmentCompleted @event, CancellationToken ct = default)
    {
        await _dashboard.HandleAsync(@event, ct);
        await _timeline.HandleAsync(@event, ct);
    }

    public async Task HandleAsync(AppointmentMissed @event, CancellationToken ct = default)
    {
        await _dashboard.HandleAsync(@event, ct);
        await _timeline.HandleAsync(@event, ct);
    }

    public Task HandleAsync(NotificationSent @event, CancellationToken ct = default)
        => _timeline.HandleAsync(@event, ct);
}
