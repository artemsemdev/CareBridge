using CareBridge.Shared.Contracts.Enums;

namespace CareBridge.Shared.Contracts.Events;

public sealed record NotificationSent : IntegrationEvent
{
    public required Guid NotificationId { get; init; }
    public required Guid CaseId { get; init; }
    public required NotificationChannel Channel { get; init; }
    public required string Recipient { get; init; }
    public required string Subject { get; init; }
    public required DateTimeOffset SentAt { get; init; }
}
