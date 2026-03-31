using CareBridge.CareGapEngine.Data;
using CareBridge.CareGapEngine.Entities;
using CareBridge.CareGapEngine.Services;
using CareBridge.Shared.Contracts.Enums;
using CareBridge.Shared.Contracts.Events;
using CareBridge.Shared.Infrastructure.Eventing;
using Microsoft.Extensions.Logging;

namespace CareBridge.CareGapEngine.Handlers;

// Authorization: System-initiated — processes ObservationReceived events under service identity.
// PHI: Evaluates patient vital sign values against clinical thresholds.
// Audit: AlertRaised event records automated clinical alert creation for compliance tracing.
// Log Hygiene: Observation values appear in debug logs only (disabled in production).
public class ObservationReceivedHandler : IEventHandler<ObservationReceived>
{
    private readonly CareGapDbContext _db;
    private readonly IEventPublisher _publisher;
    private readonly ILogger<ObservationReceivedHandler> _logger;

    public ObservationReceivedHandler(CareGapDbContext db, IEventPublisher publisher, ILogger<ObservationReceivedHandler> logger)
    {
        _db = db;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task HandleAsync(ObservationReceived @event, CancellationToken ct = default)
    {
        var alertInfo = ThresholdEvaluator.Evaluate(@event.Type, @event.Value);

        if (alertInfo is null)
        {
            _logger.LogDebug("Observation {ObservationId} type={Type} value={Value} is within normal range, no alert created.",
                @event.ObservationId, @event.Type, @event.Value);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var alert = new Alert
        {
            Id = Guid.NewGuid(),
            CaseId = @event.CaseId,
            Type = AlertType.AbnormalReading,
            Severity = alertInfo.Severity,
            Title = alertInfo.Title,
            Description = alertInfo.Description,
            SourceEventId = @event.ObservationId,
            Status = AlertStatus.Open,
            CreatedAt = now
        };

        _db.Alerts.Add(alert);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Alert {AlertId} created for case {CaseId}: [{Severity}] {Title}",
            alert.Id, alert.CaseId, alert.Severity, alert.Title);

        try
        {
            await _publisher.PublishAsync(new AlertRaised
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
            _logger.LogWarning(ex, "Failed to publish AlertRaised event for alert {AlertId}.", alert.Id);
        }
    }
}
