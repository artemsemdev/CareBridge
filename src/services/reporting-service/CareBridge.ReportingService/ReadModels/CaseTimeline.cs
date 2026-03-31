namespace CareBridge.ReportingService.ReadModels;

public class CaseTimeline
{
    public Guid CaseId { get; set; }
    public List<TimelineEntry> Entries { get; set; } = [];
}

public class TimelineEntry
{
    public Guid Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public string? Severity { get; set; }
    public Guid? EntityId { get; set; }
}
