using CareBridge.ReportingService.ReadModels;
using CareBridge.ReportingService.Store;

namespace CareBridge.ReportingService.UnitTests;

public class InMemoryReadModelStoreTests
{
    private readonly InMemoryReadModelStore _store = new();

    [Fact]
    public void GetDashboardSummary_ReturnsDefaults_WhenEmpty()
    {
        var summary = _store.GetDashboardSummary();

        Assert.Equal(0, summary.ActiveCaseCount);
        Assert.Equal(0, summary.AlertsByStatus.Open);
        Assert.Equal(0, summary.OpenTaskCount);
        Assert.Equal(0, summary.PendingAppointmentCount);
        Assert.Empty(summary.RecentCases);
        Assert.Empty(summary.TopAlerts);
    }

    [Fact]
    public void UpdateDashboardSummary_ModifiesState()
    {
        _store.UpdateDashboardSummary(d => d.ActiveCaseCount = 5);

        var summary = _store.GetDashboardSummary();
        Assert.Equal(5, summary.ActiveCaseCount);
    }

    [Fact]
    public void GetDashboardSummary_ReturnsSnapshot_NotReference()
    {
        _store.UpdateDashboardSummary(d => d.ActiveCaseCount = 3);
        var snapshot1 = _store.GetDashboardSummary();

        _store.UpdateDashboardSummary(d => d.ActiveCaseCount = 10);
        var snapshot2 = _store.GetDashboardSummary();

        Assert.Equal(3, snapshot1.ActiveCaseCount);
        Assert.Equal(10, snapshot2.ActiveCaseCount);
    }

    [Fact]
    public void GetOrCreateTimeline_ReturnsEmptyTimeline_WhenCaseNotFound()
    {
        var caseId = Guid.NewGuid();
        var timeline = _store.GetOrCreateTimeline(caseId);

        Assert.Equal(caseId, timeline.CaseId);
        Assert.Empty(timeline.Entries);
    }

    [Fact]
    public void AddTimelineEntry_AppearsInTimeline()
    {
        var caseId = Guid.NewGuid();
        var entry = new TimelineEntry
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            EventType = "CaseCreated",
            Category = "case",
            Title = "Case created",
            Description = "Test case",
            Actor = "system"
        };

        _store.AddTimelineEntry(caseId, entry);
        var timeline = _store.GetOrCreateTimeline(caseId);

        Assert.Single(timeline.Entries);
        Assert.Equal("CaseCreated", timeline.Entries[0].EventType);
    }

    [Fact]
    public void Timeline_ReturnedSortedByTimestampDescending()
    {
        var caseId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        _store.AddTimelineEntry(caseId, new TimelineEntry
        {
            Id = Guid.NewGuid(),
            Timestamp = now.AddHours(-2),
            EventType = "First",
            Category = "case",
            Title = "First",
            Description = "",
            Actor = "system"
        });
        _store.AddTimelineEntry(caseId, new TimelineEntry
        {
            Id = Guid.NewGuid(),
            Timestamp = now,
            EventType = "Third",
            Category = "case",
            Title = "Third",
            Description = "",
            Actor = "system"
        });
        _store.AddTimelineEntry(caseId, new TimelineEntry
        {
            Id = Guid.NewGuid(),
            Timestamp = now.AddHours(-1),
            EventType = "Second",
            Category = "case",
            Title = "Second",
            Description = "",
            Actor = "system"
        });

        var timeline = _store.GetOrCreateTimeline(caseId);

        Assert.Equal(3, timeline.Entries.Count);
        Assert.Equal("Third", timeline.Entries[0].EventType);
        Assert.Equal("Second", timeline.Entries[1].EventType);
        Assert.Equal("First", timeline.Entries[2].EventType);
    }

    [Fact]
    public void EventDedup_TracksProcessedEvents()
    {
        var eventId = Guid.NewGuid();

        Assert.False(_store.HasProcessedEvent(eventId));

        _store.MarkEventProcessed(eventId);

        Assert.True(_store.HasProcessedEvent(eventId));
    }

    [Fact]
    public void EventDedup_DoubleMarkIsIdempotent()
    {
        var eventId = Guid.NewGuid();

        _store.MarkEventProcessed(eventId);
        _store.MarkEventProcessed(eventId);

        Assert.True(_store.HasProcessedEvent(eventId));
    }
}
