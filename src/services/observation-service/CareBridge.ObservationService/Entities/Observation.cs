using CareBridge.Shared.Contracts.Enums;

namespace CareBridge.ObservationService.Entities;

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
