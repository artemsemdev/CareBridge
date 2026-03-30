using CareBridge.Shared.Contracts.Enums;

namespace CareBridge.TaskService.UnitTests;

public class AlertToTaskMappingTests
{
    // Replicate priority mapping from AlertRaisedHandler for pure unit testing
    private static TaskPriority MapPriority(Severity severity) => severity switch
    {
        Severity.Critical => TaskPriority.Urgent,
        Severity.High => TaskPriority.High,
        Severity.Medium => TaskPriority.Medium,
        Severity.Informational => TaskPriority.Low,
        _ => TaskPriority.Medium
    };

    [Theory]
    [InlineData(Severity.Critical, TaskPriority.Urgent)]
    [InlineData(Severity.High, TaskPriority.High)]
    [InlineData(Severity.Medium, TaskPriority.Medium)]
    [InlineData(Severity.Informational, TaskPriority.Low)]
    public void SeverityMapsToCorrectPriority(Severity severity, TaskPriority expectedPriority)
    {
        Assert.Equal(expectedPriority, MapPriority(severity));
    }

    // Replicate title generation from AlertRaisedHandler
    private static string GenerateTitle(AlertType alertType, Severity severity, string title, Guid caseId) => alertType switch
    {
        AlertType.AbnormalReading => $"Review {severity} {title} for case {caseId}",
        AlertType.MissedMilestone => $"Follow up on missed milestone: {title} for case {caseId}",
        _ => $"Alert follow-up: {title} for case {caseId}"
    };

    [Fact]
    public void AbnormalReading_GeneratesDescriptiveTitle()
    {
        var caseId = Guid.NewGuid();
        var title = GenerateTitle(AlertType.AbnormalReading, Severity.Critical, "Critical BP Reading", caseId);
        Assert.Contains("Review", title);
        Assert.Contains("Critical", title);
        Assert.Contains(caseId.ToString(), title);
    }

    [Fact]
    public void MissedMilestone_GeneratesDescriptiveTitle()
    {
        var caseId = Guid.NewGuid();
        var title = GenerateTitle(AlertType.MissedMilestone, Severity.Medium, "Follow-Up Appointment", caseId);
        Assert.Contains("Follow up on missed milestone", title);
        Assert.Contains("Follow-Up Appointment", title);
        Assert.Contains(caseId.ToString(), title);
    }
}
