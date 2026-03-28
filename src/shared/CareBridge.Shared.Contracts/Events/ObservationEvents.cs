using CareBridge.Shared.Contracts.Enums;

namespace CareBridge.Shared.Contracts.Events;

public sealed record ObservationReceived : IntegrationEvent
{
    public required Guid ObservationId { get; init; }
    public required Guid CaseId { get; init; }
    public required ObservationType Type { get; init; }
    public required decimal Value { get; init; }
    public required string Unit { get; init; }
    public required DateTimeOffset RecordedAt { get; init; }
}
