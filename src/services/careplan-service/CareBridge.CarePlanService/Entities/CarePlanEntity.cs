using CareBridge.Shared.Contracts.Enums;

namespace CareBridge.CarePlanService.Entities;

public class CarePlanEntity
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public CarePlanStatus Status { get; set; }
    public DateTimeOffset ActivatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public List<MilestoneEntity> Milestones { get; set; } = [];
}
