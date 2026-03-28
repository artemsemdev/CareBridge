namespace CareBridge.Shared.Contracts.Events;

public sealed record CarePlanActivated : IntegrationEvent
{
    public required Guid CarePlanId { get; init; }
    public required Guid CaseId { get; init; }
    public required string TemplateName { get; init; }
    public required int MilestoneCount { get; init; }
    public required DateTimeOffset ActivatedAt { get; init; }
}

public sealed record MilestoneCompleted : IntegrationEvent
{
    public required Guid MilestoneId { get; init; }
    public required Guid CarePlanId { get; init; }
    public required Guid CaseId { get; init; }
    public required string MilestoneName { get; init; }
    public required DateTimeOffset CompletedAt { get; init; }
}
