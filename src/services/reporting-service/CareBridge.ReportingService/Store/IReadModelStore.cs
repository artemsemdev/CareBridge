using CareBridge.ReportingService.ReadModels;

namespace CareBridge.ReportingService.Store;

// Retention: Read model store is reconstructed from events. No hard-delete operations exist.
// In production (Cosmos DB), read models follow the same retention policy as source events.
public interface IReadModelStore
{
    DashboardSummary GetDashboardSummary();
    void UpdateDashboardSummary(Action<DashboardSummary> update);

    CaseTimeline GetOrCreateTimeline(Guid caseId);
    void AddTimelineEntry(Guid caseId, TimelineEntry entry);

    bool HasProcessedEvent(Guid eventId);
    void MarkEventProcessed(Guid eventId);
}
