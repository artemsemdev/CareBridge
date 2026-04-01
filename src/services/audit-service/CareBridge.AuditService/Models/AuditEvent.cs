namespace CareBridge.AuditService.Models;

// Audit: Immutable record of a domain event. Once created, no field is ever modified.
// HIPAA §164.312(b): Payload may contain clinical data — access must be audited and role-gated in production.
public class AuditEvent
{
    public Guid Id { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string ActorId { get; init; } = string.Empty;
    public string ActorRole { get; init; } = string.Empty;
    public string EntityType { get; init; } = string.Empty;
    public Guid EntityId { get; init; }
    public Guid CaseId { get; init; }
    public string ServiceSource { get; init; } = string.Empty;
    public string Payload { get; init; } = string.Empty;
    public Guid EventId { get; init; }
}
