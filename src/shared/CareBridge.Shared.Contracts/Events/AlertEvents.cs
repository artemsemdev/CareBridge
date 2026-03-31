using CareBridge.Shared.Contracts.Enums;

namespace CareBridge.Shared.Contracts.Events;

// HIPAA Minimum Necessary: Alert events carry clinical context (severity, type, description)
// but NOT patient demographics. Services reference cases by CaseId only.
public sealed record AlertRaised : IntegrationEvent
{
    public required Guid AlertId { get; init; }
    public required Guid CaseId { get; init; }
    public required AlertType AlertType { get; init; }
    public required Severity Severity { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public Guid? SourceEventId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

public sealed record AlertAcknowledged : IntegrationEvent
{
    public required Guid AlertId { get; init; }
    public required Guid CaseId { get; init; }
    public required string AcknowledgedBy { get; init; }
    public required DateTimeOffset AcknowledgedAt { get; init; }
}

public sealed record AlertResolved : IntegrationEvent
{
    public required Guid AlertId { get; init; }
    public required Guid CaseId { get; init; }
    public required string ResolvedBy { get; init; }
    public required DateTimeOffset ResolvedAt { get; init; }
}
