using System.Text.Json;
using CareBridge.CareGapEngine.Data;
using CareBridge.CareGapEngine.Entities;
using CareBridge.Shared.Contracts.Enums;
using CareBridge.Shared.Contracts.Events;
using CareBridge.Shared.Contracts.Serialization;
using CareBridge.Shared.Infrastructure.Eventing;
using Microsoft.EntityFrameworkCore;

namespace CareBridge.CareGapEngine.Services;

public class MilestoneScanBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MilestoneScanBackgroundService> _logger;
    private readonly TimeSpan _scanInterval;

    public MilestoneScanBackgroundService(
        IServiceProvider serviceProvider,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<MilestoneScanBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        var intervalMinutes = configuration.GetValue<int>("MilestoneScan:IntervalMinutes", 15);
        _scanInterval = TimeSpan.FromMinutes(intervalMinutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MilestoneScanBackgroundService started. Interval: {Interval}", _scanInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunScanAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error during milestone scan.");
            }

            await Task.Delay(_scanInterval, stoppingToken);
        }
    }

    private async Task RunScanAsync(CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("CarePlanService");
        List<CarePlanDto>? activePlans;
        try
        {
            var response = await client.GetAsync("/api/v1/care-plans?status=Active", ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Care Plan Service returned {StatusCode} for active plans query.", response.StatusCode);
                return;
            }
            var json = await response.Content.ReadAsStringAsync(ct);
            activePlans = JsonSerializer.Deserialize<List<CarePlanDto>>(json, JsonDefaults.Options);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch active care plans from Care Plan Service.");
            return;
        }

        if (activePlans is null || activePlans.Count == 0)
        {
            _logger.LogDebug("No active care plans found during milestone scan.");
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var scannedPlans = 0;
        var overdueCount = 0;
        var createdAlerts = 0;

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareGapDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

        foreach (var plan in activePlans)
        {
            scannedPlans++;
            foreach (var milestone in plan.Milestones ?? [])
            {
                // Only check Pending milestones that are past due
                if (!string.Equals(milestone.Status, "Pending", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (milestone.DueAt >= now)
                    continue;

                overdueCount++;

                // Dedup: check if alert already exists for this milestone
                var exists = await db.Alerts.AnyAsync(
                    a => a.SourceEventId == milestone.Id && a.Type == AlertType.MissedMilestone, ct);

                if (exists)
                    continue;

                var alert = new Alert
                {
                    Id = Guid.NewGuid(),
                    CaseId = plan.CaseId,
                    Type = AlertType.MissedMilestone,
                    Severity = Severity.High,
                    Title = "Missed Milestone",
                    Description = $"Milestone '{milestone.Name}' was due on {milestone.DueAt:yyyy-MM-dd HH:mm} UTC for case {plan.CaseId}.",
                    SourceEventId = milestone.Id,
                    Status = AlertStatus.Open,
                    CreatedAt = now
                };

                db.Alerts.Add(alert);
                await db.SaveChangesAsync(ct);
                createdAlerts++;

                try
                {
                    await publisher.PublishAsync(new AlertRaised
                    {
                        AlertId = alert.Id,
                        CaseId = alert.CaseId,
                        AlertType = alert.Type,
                        Severity = alert.Severity,
                        Title = alert.Title,
                        Description = alert.Description,
                        SourceEventId = alert.SourceEventId,
                        CreatedAt = alert.CreatedAt
                    }, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to publish AlertRaised for missed milestone alert {AlertId}.", alert.Id);
                }
            }
        }

        _logger.LogInformation(
            "Milestone scan complete. Scanned {Plans} active plans, found {Overdue} overdue milestones, created {Created} new alerts.",
            scannedPlans, overdueCount, createdAlerts);
    }

    // DTOs for deserializing Care Plan Service responses
    private record CarePlanDto(Guid Id, Guid CaseId, string TemplateName, string Status, List<MilestoneDto>? Milestones);
    private record MilestoneDto(Guid Id, string Name, string Description, DateTimeOffset DueAt, string Status, DateTimeOffset? CompletedAt, bool IsOverdue);
}
