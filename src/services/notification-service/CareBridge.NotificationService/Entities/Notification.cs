using CareBridge.Shared.Contracts.Enums;

namespace CareBridge.NotificationService.Entities;

public class Notification
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public NotificationChannel Channel { get; set; }
    public string Recipient { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public NotificationStatus Status { get; set; }
    public string TriggeredBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
