using CareBridge.ReportingService.ReadModels;
using CareBridge.ReportingService.Store;
using CareBridge.Shared.Contracts.Events;
using CareBridge.Shared.Infrastructure.Eventing;

namespace CareBridge.ReportingService.Handlers;

// Authorization: System-initiated — processes domain events under service identity to build dashboard read models.
// PHI: CaseCreated events carry PatientName for the RecentCases display widget.
// No other patient demographics are stored in the dashboard summary.
public class DashboardEventHandler :
    IEventHandler<CaseCreated>,
    IEventHandler<CaseUpdated>,
    IEventHandler<AlertRaised>,
    IEventHandler<AlertAcknowledged>,
    IEventHandler<AlertResolved>,
    IEventHandler<TaskCreated>,
    IEventHandler<TaskCompleted>,
    IEventHandler<AppointmentBooked>,
    IEventHandler<AppointmentCompleted>,
    IEventHandler<AppointmentMissed>
{
    private readonly IReadModelStore _store;
    private readonly ILogger<DashboardEventHandler> _logger;

    private static readonly string[] SeverityOrder = ["Critical", "High", "Medium", "Informational"];

    public DashboardEventHandler(IReadModelStore store, ILogger<DashboardEventHandler> logger)
    {
        _store = store;
        _logger = logger;
    }

    public Task HandleAsync(CaseCreated @event, CancellationToken ct = default)
    {
        if (!TryDedup(@event)) return Task.CompletedTask;

        _store.UpdateDashboardSummary(d =>
        {
            d.ActiveCaseCount++;
            d.RecentCases.Insert(0, new RecentCase
            {
                Id = @event.CaseId,
                PatientName = @event.PatientName,
                Status = @event.Status.ToString(),
                DischargeDate = @event.DischargeDate,
                CreatedAt = @event.OccurredAt
            });
            if (d.RecentCases.Count > 10) d.RecentCases.RemoveRange(10, d.RecentCases.Count - 10);
        });

        _logger.LogInformation("[Reporting] Processed CaseCreated for case {CaseId}", @event.CaseId);
        return Task.CompletedTask;
    }

    public Task HandleAsync(CaseUpdated @event, CancellationToken ct = default)
    {
        if (!TryDedup(@event)) return Task.CompletedTask;

        if (@event.Status == Shared.Contracts.Enums.CaseStatus.Closed)
        {
            _store.UpdateDashboardSummary(d =>
            {
                d.ActiveCaseCount = Math.Max(0, d.ActiveCaseCount - 1);
                var existing = d.RecentCases.Find(c => c.Id == @event.CaseId);
                if (existing is not null) existing.Status = @event.Status.ToString();
            });
        }
        else
        {
            _store.UpdateDashboardSummary(d =>
            {
                var existing = d.RecentCases.Find(c => c.Id == @event.CaseId);
                if (existing is not null) existing.Status = @event.Status.ToString();
            });
        }

        _logger.LogInformation("[Reporting] Processed CaseUpdated for case {CaseId}", @event.CaseId);
        return Task.CompletedTask;
    }

    public Task HandleAsync(AlertRaised @event, CancellationToken ct = default)
    {
        if (!TryDedup(@event)) return Task.CompletedTask;

        _store.UpdateDashboardSummary(d =>
        {
            d.AlertsByStatus.Open++;
            IncrementSeverity(d.AlertsBySeverity, @event.Severity.ToString());

            d.TopAlerts.Add(new TopAlert
            {
                Id = @event.AlertId,
                CaseId = @event.CaseId,
                Title = @event.Title,
                Severity = @event.Severity.ToString(),
                CreatedAt = @event.CreatedAt
            });

            d.TopAlerts = [.. d.TopAlerts
                .OrderBy(a => Array.IndexOf(SeverityOrder, a.Severity))
                .ThenBy(a => a.CreatedAt)
                .Take(10)];
        });

        _logger.LogInformation("[Reporting] Processed AlertRaised for case {CaseId}", @event.CaseId);
        return Task.CompletedTask;
    }

    public Task HandleAsync(AlertAcknowledged @event, CancellationToken ct = default)
    {
        if (!TryDedup(@event)) return Task.CompletedTask;

        _store.UpdateDashboardSummary(d =>
        {
            d.AlertsByStatus.Open = Math.Max(0, d.AlertsByStatus.Open - 1);
            d.AlertsByStatus.Acknowledged++;
        });

        _logger.LogInformation("[Reporting] Processed AlertAcknowledged for case {CaseId}", @event.CaseId);
        return Task.CompletedTask;
    }

    public Task HandleAsync(AlertResolved @event, CancellationToken ct = default)
    {
        if (!TryDedup(@event)) return Task.CompletedTask;

        _store.UpdateDashboardSummary(d =>
        {
            d.AlertsByStatus.Acknowledged = Math.Max(0, d.AlertsByStatus.Acknowledged - 1);
            d.TopAlerts.RemoveAll(a => a.Id == @event.AlertId);
        });

        _logger.LogInformation("[Reporting] Processed AlertResolved for case {CaseId}", @event.CaseId);
        return Task.CompletedTask;
    }

    public Task HandleAsync(TaskCreated @event, CancellationToken ct = default)
    {
        if (!TryDedup(@event)) return Task.CompletedTask;

        _store.UpdateDashboardSummary(d =>
        {
            d.OpenTaskCount++;
        });

        _logger.LogInformation("[Reporting] Processed TaskCreated for case {CaseId}", @event.CaseId);
        return Task.CompletedTask;
    }

    public Task HandleAsync(TaskCompleted @event, CancellationToken ct = default)
    {
        if (!TryDedup(@event)) return Task.CompletedTask;

        _store.UpdateDashboardSummary(d =>
        {
            d.OpenTaskCount = Math.Max(0, d.OpenTaskCount - 1);
        });

        _logger.LogInformation("[Reporting] Processed TaskCompleted for case {CaseId}", @event.CaseId);
        return Task.CompletedTask;
    }

    public Task HandleAsync(AppointmentBooked @event, CancellationToken ct = default)
    {
        if (!TryDedup(@event)) return Task.CompletedTask;

        _store.UpdateDashboardSummary(d => d.PendingAppointmentCount++);

        _logger.LogInformation("[Reporting] Processed AppointmentBooked for case {CaseId}", @event.CaseId);
        return Task.CompletedTask;
    }

    public Task HandleAsync(AppointmentCompleted @event, CancellationToken ct = default)
    {
        if (!TryDedup(@event)) return Task.CompletedTask;

        _store.UpdateDashboardSummary(d => d.PendingAppointmentCount = Math.Max(0, d.PendingAppointmentCount - 1));

        _logger.LogInformation("[Reporting] Processed AppointmentCompleted for case {CaseId}", @event.CaseId);
        return Task.CompletedTask;
    }

    public Task HandleAsync(AppointmentMissed @event, CancellationToken ct = default)
    {
        if (!TryDedup(@event)) return Task.CompletedTask;

        _store.UpdateDashboardSummary(d => d.PendingAppointmentCount = Math.Max(0, d.PendingAppointmentCount - 1));

        _logger.LogInformation("[Reporting] Processed AppointmentMissed for case {CaseId}", @event.CaseId);
        return Task.CompletedTask;
    }

    private bool TryDedup(IntegrationEvent @event)
    {
        if (_store.HasProcessedEvent(@event.EventId))
        {
            _logger.LogDebug("[Reporting] Skipping duplicate event {EventId} ({EventType})", @event.EventId, @event.GetType().Name);
            return false;
        }
        _store.MarkEventProcessed(@event.EventId);
        return true;
    }

    private static void IncrementSeverity(AlertSeverityCounts counts, string severity)
    {
        switch (severity)
        {
            case "Critical": counts.Critical++; break;
            case "High": counts.High++; break;
            case "Medium": counts.Medium++; break;
            case "Informational": counts.Informational++; break;
        }
    }
}
