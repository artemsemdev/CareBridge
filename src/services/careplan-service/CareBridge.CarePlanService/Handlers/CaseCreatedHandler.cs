using CareBridge.CarePlanService.Data;
using CareBridge.CarePlanService.Entities;
using CareBridge.CarePlanService.Templates;
using CareBridge.Shared.Contracts.Enums;
using CareBridge.Shared.Contracts.Events;
using CareBridge.Shared.Infrastructure.Eventing;
using Microsoft.EntityFrameworkCore;

namespace CareBridge.CarePlanService.Handlers;

public class CaseCreatedHandler : IEventHandler<CaseCreated>
{
    private readonly CarePlanDbContext _db;
    private readonly IEventPublisher _publisher;
    private readonly ILogger<CaseCreatedHandler> _logger;

    public CaseCreatedHandler(CarePlanDbContext db, IEventPublisher publisher, ILogger<CaseCreatedHandler> logger)
    {
        _db = db;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task HandleAsync(CaseCreated @event, CancellationToken ct = default)
    {
        var exists = await _db.CarePlans.AnyAsync(p => p.CaseId == @event.CaseId, ct);
        if (exists)
        {
            _logger.LogInformation("Care plan already exists for case {CaseId}, skipping.", @event.CaseId);
            return;
        }

        var template = CarePlanTemplates.Resolve(@event.DiagnosisCode);
        var activatedAt = DateTimeOffset.UtcNow;

        var plan = new CarePlanEntity
        {
            Id = Guid.NewGuid(),
            CaseId = @event.CaseId,
            TemplateName = template.Name,
            Status = CarePlanStatus.Active,
            ActivatedAt = activatedAt,
            Milestones = template.MilestoneDefinitions.Select(def => new MilestoneEntity
            {
                Id = Guid.NewGuid(),
                Name = def.Name,
                Description = def.Description,
                DueWithinHours = def.DueWithinHours,
                DueAt = activatedAt.AddHours(def.DueWithinHours),
                Status = MilestoneStatus.Pending
            }).ToList()
        };

        _db.CarePlans.Add(plan);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Activated care plan {PlanId} for case {CaseId} with {Count} milestones.",
            plan.Id, plan.CaseId, plan.Milestones.Count);

        try
        {
            await _publisher.PublishAsync(new CarePlanActivated
            {
                CarePlanId = plan.Id,
                CaseId = plan.CaseId,
                TemplateName = plan.TemplateName,
                MilestoneCount = plan.Milestones.Count,
                ActivatedAt = plan.ActivatedAt
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish CarePlanActivated event for plan {PlanId}.", plan.Id);
        }
    }
}
