using CareBridge.Shared.Contracts.Enums;

namespace CareBridge.ObservationService.Entities;

// PHI: This entity contains clinical observation data (vital sign values).
// Observation values are Protected Health Information under HIPAA.
// Log Hygiene: Never log the Value property — log ObservationId and Type only.
public class Observation
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public ObservationType Type { get; set; }
    public decimal Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public string? DeviceId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
}
