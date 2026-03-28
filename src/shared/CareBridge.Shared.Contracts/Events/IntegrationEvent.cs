namespace CareBridge.Shared.Contracts.Events;

public abstract record IntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public string CorrelationId { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public int Version { get; init; } = 1;

    protected IntegrationEvent()
    {
        EventType = GetType().FullName ?? GetType().Name;
    }
}
