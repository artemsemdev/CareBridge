namespace CareBridge.AuditService.Models;

public class AuditQueryFilter
{
    public Guid? CaseId { get; set; }
    public string? ActorId { get; set; }
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public string? EventType { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public int Limit { get; set; } = 50;
    public string? Cursor { get; set; }
}
