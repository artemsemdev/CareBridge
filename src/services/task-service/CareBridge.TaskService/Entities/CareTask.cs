using CareBridge.Shared.Contracts.Enums;

namespace CareBridge.TaskService.Entities;

public class CareTask
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public Guid? AlertId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? AssignedTo { get; set; }
    public CareBridgeTaskStatus Status { get; set; }
    public TaskPriority Priority { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? CompletedBy { get; set; }
}
