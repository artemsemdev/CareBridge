using CareBridge.Shared.Contracts.Enums;

namespace CareBridge.Shared.Contracts.Events;

// HIPAA Minimum Necessary: NotificationSent carries delivery metadata (channel, recipient, subject)
// but NOT the notification body, to limit PHI exposure if the message bus is compromised.
// Security: Notification body is excluded from event payload to limit blast radius.
public sealed record NotificationSent : IntegrationEvent
{
    public required Guid NotificationId { get; init; }
    public required Guid CaseId { get; init; }
    public required NotificationChannel Channel { get; init; }
    public required string Recipient { get; init; }
    public required string Subject { get; init; }
    public required DateTimeOffset SentAt { get; init; }
}
