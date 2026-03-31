using CareBridge.CarePlanService.Data;
using CareBridge.Shared.Contracts.Enums;
using CareBridge.Shared.Contracts.Events;
using CareBridge.Shared.Infrastructure.Eventing;
using Microsoft.EntityFrameworkCore;

namespace CareBridge.CarePlanService.Handlers;

// Authorization: System-initiated — processes AppointmentCompleted events under service identity.
// Audit: MilestoneCompleted event records automated milestone completion triggered by appointment.
public class AppointmentCompletedHandler : IEventHandler<AppointmentCompleted>
{
    private readonly CarePlanDbContext _db;
    private readonly IEventPublisher _publisher;
    private readonly ILogger<AppointmentCompletedHandler> _logger;

    public AppointmentCompletedHandler(CarePlanDbContext db, IEventPublisher publisher, ILogger<AppointmentCompletedHandler> logger)
    {
        _db = db;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task HandleAsync(AppointmentCompleted @event, CancellationToken ct = default)
    {
        var plan = await _db.CarePlans
            .Include(p => p.Milestones)
            .FirstOrDefaultAsync(p => p.CaseId == @event.CaseId, ct);

        if (plan is null)
        {
            _logger.LogInformation("No care plan found for case {CaseId}, skipping.", @event.CaseId);
            return;
        }

        var milestone = plan.Milestones
            .FirstOrDefault(m => m.Name == "Follow-Up Appointment" && m.Status == MilestoneStatus.Pending);

        if (milestone is null)
        {
            _logger.LogInformation("No pending Follow-Up Appointment milestone for case {CaseId}, skipping.", @event.CaseId);
            return;
        }

        milestone.Status = MilestoneStatus.Completed;
        milestone.CompletedAt = DateTimeOffset.UtcNow;

        if (plan.Milestones.All(m => m.Status is MilestoneStatus.Completed or MilestoneStatus.Skipped))
        {
            plan.Status = CarePlanStatus.Completed;
            plan.CompletedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Follow-Up Appointment milestone completed for case {CaseId}.", @event.CaseId);

        try
        {
            await _publisher.PublishAsync(new MilestoneCompleted
            {
                MilestoneId = milestone.Id,
                CarePlanId = plan.Id,
                CaseId = plan.CaseId,
                MilestoneName = milestone.Name,
                CompletedAt = milestone.CompletedAt!.Value
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish MilestoneCompleted event for milestone {MilestoneId}.", milestone.Id);
        }
    }
}
