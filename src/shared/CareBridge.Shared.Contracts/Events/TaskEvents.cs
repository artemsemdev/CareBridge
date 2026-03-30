using CareBridge.Shared.Contracts.Enums;

namespace CareBridge.Shared.Contracts.Events;

public sealed record TaskCreated : IntegrationEvent
{
    public required Guid TaskId { get; init; }
    public required Guid CaseId { get; init; }
    public Guid? AlertId { get; init; }
    public required string Title { get; init; }
    public required TaskPriority Priority { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

public sealed record TaskCompleted : IntegrationEvent
{
    public required Guid TaskId { get; init; }
    public required Guid CaseId { get; init; }
    public Guid? AlertId { get; init; }
    public required string CompletedBy { get; init; }
    public required DateTimeOffset CompletedAt { get; init; }
}
