using CareBridge.Shared.Contracts.Events;

namespace CareBridge.Shared.Infrastructure.Eventing;

public interface IEventHandler<in T> where T : IntegrationEvent
{
    Task HandleAsync(T @event, CancellationToken ct = default);
}
