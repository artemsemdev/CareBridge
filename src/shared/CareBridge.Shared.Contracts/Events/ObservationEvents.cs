using CareBridge.Shared.Contracts.Enums;

namespace CareBridge.Shared.Contracts.Events;

// PHI: ObservationReceived carries clinical vital sign values (Value, Unit, Type).
// HIPAA Minimum Necessary: Value is required for threshold evaluation in CareGap Engine.
// Log Hygiene: Consuming services must not log the Value field — it is clinical PHI.
public sealed record ObservationReceived : IntegrationEvent
{
    public required Guid ObservationId { get; init; }
    public required Guid CaseId { get; init; }
    public required ObservationType Type { get; init; }
    public required decimal Value { get; init; }
    public required string Unit { get; init; }
    public required DateTimeOffset RecordedAt { get; init; }
}
