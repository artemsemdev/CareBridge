using CareBridge.Shared.Contracts.Enums;

namespace CareBridge.CarePlanService.Entities;

public class MilestoneEntity
{
    public Guid Id { get; set; }
    public Guid CarePlanId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int DueWithinHours { get; set; }
    public DateTimeOffset DueAt { get; set; }
    public MilestoneStatus Status { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public CarePlanEntity CarePlan { get; set; } = null!;
}
