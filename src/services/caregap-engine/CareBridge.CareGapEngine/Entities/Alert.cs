using CareBridge.Shared.Contracts.Enums;

namespace CareBridge.CareGapEngine.Entities;

public class Alert
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public AlertType Type { get; set; }
    public Severity Severity { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid? SourceEventId { get; set; }
    public AlertStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? AcknowledgedAt { get; set; }
    public string? AcknowledgedBy { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }
}
