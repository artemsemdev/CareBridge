namespace CareBridge.ReportingService.ReadModels;

// PHI: RecentCase.PatientName contains patient-identifiable data for coordinator display.
// HIPAA Minimum Necessary: Dashboard read models store only the minimum fields needed
// for aggregate metrics. No contact info, clinical values, or diagnosis details are stored.
public class DashboardSummary
{
    public int ActiveCaseCount { get; set; }
    public AlertStatusCounts AlertsByStatus { get; set; } = new();
    public AlertSeverityCounts AlertsBySeverity { get; set; } = new();
    public int OverdueTaskCount { get; set; }
    public int OpenTaskCount { get; set; }
    public int PendingAppointmentCount { get; set; }
    public List<RecentCase> RecentCases { get; set; } = [];
    public List<TopAlert> TopAlerts { get; set; } = [];
    public DateTimeOffset LastUpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class AlertStatusCounts
{
    public int Open { get; set; }
    public int Acknowledged { get; set; }
}

public class AlertSeverityCounts
{
    public int Critical { get; set; }
    public int High { get; set; }
    public int Medium { get; set; }
    public int Informational { get; set; }
}

public class RecentCase
{
    public Guid Id { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset DischargeDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class TopAlert
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

public class TrackedTask
{
    public Guid TaskId { get; set; }
    public Guid CaseId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsCompleted { get; set; }
}
