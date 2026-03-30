using CareBridge.Shared.Contracts.Enums;
using CareBridge.Shared.Contracts.Events;
using CareBridge.Shared.Infrastructure.Eventing;
using CareBridge.TaskService.Data;
using CareBridge.TaskService.Entities;
using Microsoft.EntityFrameworkCore;

namespace CareBridge.TaskService.Handlers;

public class AlertRaisedHandler : IEventHandler<AlertRaised>
{
    private readonly TaskDbContext _db;
    private readonly IEventPublisher _publisher;
    private readonly ILogger<AlertRaisedHandler> _logger;

    public AlertRaisedHandler(TaskDbContext db, IEventPublisher publisher, ILogger<AlertRaisedHandler> logger)
    {
        _db = db;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task HandleAsync(AlertRaised @event, CancellationToken ct = default)
    {
        var exists = await _db.CareTasks.AnyAsync(t => t.AlertId == @event.AlertId, ct);
        if (exists)
        {
            _logger.LogInformation("Task already exists for alert {AlertId}, skipping.", @event.AlertId);
            return;
        }

        var priority = MapPriority(@event.Severity);
        var title = GenerateTitle(@event);
        var now = DateTimeOffset.UtcNow;

        var task = new CareTask
        {
            Id = Guid.NewGuid(),
            CaseId = @event.CaseId,
            AlertId = @event.AlertId,
            Title = title,
            Description = @event.Description,
            AssignedTo = null,
            Status = CareBridgeTaskStatus.Open,
            Priority = priority,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.CareTasks.Add(task);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Created task {TaskId} from alert {AlertId} with priority {Priority}.",
            task.Id, @event.AlertId, priority);

        try
        {
            await _publisher.PublishAsync(new TaskCreated
            {
                TaskId = task.Id,
                CaseId = task.CaseId,
                AlertId = task.AlertId,
                Title = task.Title,
                Priority = task.Priority,
                CreatedAt = task.CreatedAt
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish TaskCreated event for task {TaskId}.", task.Id);
        }
    }

    private static TaskPriority MapPriority(Severity severity) => severity switch
    {
        Severity.Critical => TaskPriority.Urgent,
        Severity.High => TaskPriority.High,
        Severity.Medium => TaskPriority.Medium,
        Severity.Informational => TaskPriority.Low,
        _ => TaskPriority.Medium
    };

    private static string GenerateTitle(AlertRaised @event) => @event.AlertType switch
    {
        AlertType.AbnormalReading => $"Review {@event.Severity} {@event.Title} for case {@event.CaseId}",
        AlertType.MissedMilestone => $"Follow up on missed milestone: {@event.Title} for case {@event.CaseId}",
        _ => $"Alert follow-up: {@event.Title} for case {@event.CaseId}"
    };
}
