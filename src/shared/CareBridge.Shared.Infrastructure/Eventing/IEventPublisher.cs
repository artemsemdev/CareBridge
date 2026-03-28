using CareBridge.Shared.Contracts.Events;

namespace CareBridge.Shared.Infrastructure.Eventing;

public interface IEventPublisher
{
    Task PublishAsync<T>(T @event, CancellationToken ct = default) where T : IntegrationEvent;
}
