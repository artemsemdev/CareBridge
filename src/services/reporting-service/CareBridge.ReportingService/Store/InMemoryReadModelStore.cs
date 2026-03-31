using System.Collections.Concurrent;
using CareBridge.ReportingService.ReadModels;

namespace CareBridge.ReportingService.Store;

public class InMemoryReadModelStore : IReadModelStore
{
    private readonly DashboardSummary _dashboard = new();
    private readonly ConcurrentDictionary<Guid, CaseTimeline> _timelines = new();
    private readonly ConcurrentDictionary<Guid, byte> _processedEvents = new();
    private readonly object _dashboardLock = new();

    public DashboardSummary GetDashboardSummary()
    {
        lock (_dashboardLock)
        {
            return new DashboardSummary
            {
                ActiveCaseCount = _dashboard.ActiveCaseCount,
                AlertsByStatus = new AlertStatusCounts
                {
                    Open = _dashboard.AlertsByStatus.Open,
                    Acknowledged = _dashboard.AlertsByStatus.Acknowledged
                },
                AlertsBySeverity = new AlertSeverityCounts
                {
                    Critical = _dashboard.AlertsBySeverity.Critical,
                    High = _dashboard.AlertsBySeverity.High,
                    Medium = _dashboard.AlertsBySeverity.Medium,
                    Informational = _dashboard.AlertsBySeverity.Informational
                },
                OverdueTaskCount = _dashboard.OverdueTaskCount,
                OpenTaskCount = _dashboard.OpenTaskCount,
                PendingAppointmentCount = _dashboard.PendingAppointmentCount,
                RecentCases = [.. _dashboard.RecentCases],
                TopAlerts = [.. _dashboard.TopAlerts],
                LastUpdatedAt = _dashboard.LastUpdatedAt
            };
        }
    }

    public void UpdateDashboardSummary(Action<DashboardSummary> update)
    {
        lock (_dashboardLock)
        {
            update(_dashboard);
            _dashboard.LastUpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    public CaseTimeline GetOrCreateTimeline(Guid caseId)
    {
        var timeline = _timelines.GetOrAdd(caseId, id => new CaseTimeline { CaseId = id });
        lock (timeline)
        {
            return new CaseTimeline
            {
                CaseId = timeline.CaseId,
                Entries = [.. timeline.Entries.OrderByDescending(e => e.Timestamp)]
            };
        }
    }

    public void AddTimelineEntry(Guid caseId, TimelineEntry entry)
    {
        var timeline = _timelines.GetOrAdd(caseId, id => new CaseTimeline { CaseId = id });
        lock (timeline)
        {
            timeline.Entries.Add(entry);
        }
    }

    public bool HasProcessedEvent(Guid eventId) => _processedEvents.ContainsKey(eventId);

    public void MarkEventProcessed(Guid eventId) => _processedEvents.TryAdd(eventId, 0);
}
