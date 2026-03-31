using CareBridge.ReportingService.ReadModels;

namespace CareBridge.ReportingService.Store;

public interface IReadModelStore
{
    DashboardSummary GetDashboardSummary();
    void UpdateDashboardSummary(Action<DashboardSummary> update);

    CaseTimeline GetOrCreateTimeline(Guid caseId);
    void AddTimelineEntry(Guid caseId, TimelineEntry entry);

    bool HasProcessedEvent(Guid eventId);
    void MarkEventProcessed(Guid eventId);
}
