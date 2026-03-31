namespace CareBridge.Shared.Contracts.Events;

// Audit: Every integration event carries EventId, OccurredAt, and CorrelationId for audit trail.
// The Audit Service consumes all event types and creates immutable audit records per HIPAA §164.312(b).
// Security: CorrelationId is for distributed tracing only — never used for authorization decisions.
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
