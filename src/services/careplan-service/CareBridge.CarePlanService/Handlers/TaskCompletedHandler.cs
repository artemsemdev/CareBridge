using System.Text.Json;
using CareBridge.CarePlanService.Data;
using CareBridge.Shared.Contracts.Enums;
using CareBridge.Shared.Contracts.Events;
using CareBridge.Shared.Infrastructure.Eventing;
using Microsoft.EntityFrameworkCore;

namespace CareBridge.CarePlanService.Handlers;

public class TaskCompletedHandler : IEventHandler<TaskCompleted>
{
    private readonly CarePlanDbContext _db;
    private readonly IEventPublisher _publisher;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TaskCompletedHandler> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public TaskCompletedHandler(CarePlanDbContext db, IEventPublisher publisher, IHttpClientFactory httpClientFactory, ILogger<TaskCompletedHandler> logger)
    {
        _db = db;
        _publisher = publisher;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task HandleAsync(TaskCompleted @event, CancellationToken ct = default)
    {
        if (@event.AlertId is null)
        {
            _logger.LogInformation("Task {TaskId} has no AlertId (manual task), skipping milestone completion.", @event.TaskId);
            return;
        }

        // Look up the alert from CareGap Engine to determine type and SourceEventId
        var client = _httpClientFactory.CreateClient("CareGapEngine");
        AlertDto? alert;
        try
        {
            var response = await client.GetAsync($"/api/v1/alerts/{@event.AlertId}", ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Could not look up alert {AlertId} — HTTP {StatusCode}.", @event.AlertId, response.StatusCode);
                return;
            }
            alert = await JsonSerializer.DeserializeAsync<AlertDto>(
                await response.Content.ReadAsStreamAsync(ct), JsonOptions, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to look up alert {AlertId} from CareGap Engine.", @event.AlertId);
            return;
        }

        if (alert is null || alert.Type != "MissedMilestone" || alert.SourceEventId is null)
        {
            _logger.LogInformation("Alert {AlertId} is not a MissedMilestone alert, skipping.", @event.AlertId);
            return;
        }

        var milestoneId = alert.SourceEventId.Value;

        var milestone = await _db.Milestones
            .Include(m => m.CarePlan)
            .ThenInclude(p => p.Milestones)
            .FirstOrDefaultAsync(m => m.Id == milestoneId, ct);

        if (milestone is null)
        {
            _logger.LogWarning("Milestone {MilestoneId} not found.", milestoneId);
            return;
        }

        if (milestone.Status is MilestoneStatus.Completed or MilestoneStatus.Skipped)
        {
            _logger.LogInformation("Milestone {MilestoneId} already in status {Status}, skipping.", milestoneId, milestone.Status);
            return;
        }

        milestone.Status = MilestoneStatus.Completed;
        milestone.CompletedAt = DateTimeOffset.UtcNow;

        var plan = milestone.CarePlan;
        if (plan.Milestones.All(m => m.Status is MilestoneStatus.Completed or MilestoneStatus.Skipped))
        {
            plan.Status = CarePlanStatus.Completed;
            plan.CompletedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Milestone {MilestoneId} completed via task {TaskId}.", milestoneId, @event.TaskId);

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

    private record AlertDto(Guid Id, Guid CaseId, string Type, string Severity, string Title, string Description, Guid? SourceEventId, string Status);
}
