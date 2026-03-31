using CareBridge.ReportingService.Handlers;
using CareBridge.ReportingService.Store;
using CareBridge.Shared.Contracts.Enums;
using CareBridge.Shared.Contracts.Events;
using Microsoft.Extensions.Logging.Abstractions;

namespace CareBridge.ReportingService.UnitTests;

public class DashboardEventHandlerTests
{
    private readonly InMemoryReadModelStore _store = new();
    private readonly DashboardEventHandler _handler;

    public DashboardEventHandlerTests()
    {
        _handler = new DashboardEventHandler(_store, NullLogger<DashboardEventHandler>.Instance);
    }

    [Fact]
    public async Task CaseCreated_IncrementsActiveCaseCount()
    {
        var @event = new CaseCreated
        {
            CaseId = Guid.NewGuid(),
            PatientId = "P001",
            PatientName = "John Doe",
            DiagnosisCode = "I50.9",
            DiagnosisDescription = "Heart failure",
            DischargeDate = DateTimeOffset.UtcNow,
            Status = CaseStatus.Active
        };

        await _handler.HandleAsync(@event);

        var summary = _store.GetDashboardSummary();
        Assert.Equal(1, summary.ActiveCaseCount);
        Assert.Single(summary.RecentCases);
        Assert.Equal("John Doe", summary.RecentCases[0].PatientName);
    }

    [Fact]
    public async Task CaseUpdated_ToClosed_DecrementsActiveCaseCount()
    {
        var caseId = Guid.NewGuid();
        await _handler.HandleAsync(new CaseCreated
        {
            CaseId = caseId,
            PatientId = "P001",
            PatientName = "John Doe",
            DiagnosisCode = "I50.9",
            DiagnosisDescription = "Heart failure",
            DischargeDate = DateTimeOffset.UtcNow,
            Status = CaseStatus.Active
        });

        await _handler.HandleAsync(new CaseUpdated
        {
            CaseId = caseId,
            Status = CaseStatus.Closed,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var summary = _store.GetDashboardSummary();
        Assert.Equal(0, summary.ActiveCaseCount);
    }

    [Fact]
    public async Task AlertRaised_IncrementsOpenCountAndSeverity()
    {
        await _handler.HandleAsync(new AlertRaised
        {
            AlertId = Guid.NewGuid(),
            CaseId = Guid.NewGuid(),
            AlertType = AlertType.AbnormalReading,
            Severity = Severity.Critical,
            Title = "High BP",
            Description = "Systolic BP 190",
            CreatedAt = DateTimeOffset.UtcNow
        });

        var summary = _store.GetDashboardSummary();
        Assert.Equal(1, summary.AlertsByStatus.Open);
        Assert.Equal(1, summary.AlertsBySeverity.Critical);
        Assert.Single(summary.TopAlerts);
    }

    [Fact]
    public async Task AlertAcknowledged_ShiftsFromOpenToAcknowledged()
    {
        var alertId = Guid.NewGuid();
        var caseId = Guid.NewGuid();

        await _handler.HandleAsync(new AlertRaised
        {
            AlertId = alertId,
            CaseId = caseId,
            AlertType = AlertType.AbnormalReading,
            Severity = Severity.High,
            Title = "Test",
            Description = "Test",
            CreatedAt = DateTimeOffset.UtcNow
        });

        await _handler.HandleAsync(new AlertAcknowledged
        {
            AlertId = alertId,
            CaseId = caseId,
            AcknowledgedBy = "coordinator",
            AcknowledgedAt = DateTimeOffset.UtcNow
        });

        var summary = _store.GetDashboardSummary();
        Assert.Equal(0, summary.AlertsByStatus.Open);
        Assert.Equal(1, summary.AlertsByStatus.Acknowledged);
    }

    [Fact]
    public async Task AlertResolved_RemovesFromTopAlerts()
    {
        var alertId = Guid.NewGuid();
        var caseId = Guid.NewGuid();

        await _handler.HandleAsync(new AlertRaised
        {
            AlertId = alertId,
            CaseId = caseId,
            AlertType = AlertType.AbnormalReading,
            Severity = Severity.Critical,
            Title = "Test",
            Description = "Test",
            CreatedAt = DateTimeOffset.UtcNow
        });

        await _handler.HandleAsync(new AlertAcknowledged
        {
            AlertId = alertId,
            CaseId = caseId,
            AcknowledgedBy = "coordinator",
            AcknowledgedAt = DateTimeOffset.UtcNow
        });

        await _handler.HandleAsync(new AlertResolved
        {
            AlertId = alertId,
            CaseId = caseId,
            ResolvedBy = "coordinator",
            ResolvedAt = DateTimeOffset.UtcNow
        });

        var summary = _store.GetDashboardSummary();
        Assert.Equal(0, summary.AlertsByStatus.Acknowledged);
        Assert.Empty(summary.TopAlerts);
    }

    [Fact]
    public async Task TaskCreated_IncrementsOpenTaskCount()
    {
        await _handler.HandleAsync(new TaskCreated
        {
            TaskId = Guid.NewGuid(),
            CaseId = Guid.NewGuid(),
            Title = "Follow up",
            Priority = TaskPriority.High,
            CreatedAt = DateTimeOffset.UtcNow
        });

        var summary = _store.GetDashboardSummary();
        Assert.Equal(1, summary.OpenTaskCount);
    }

    [Fact]
    public async Task TaskCompleted_DecrementsOpenTaskCount()
    {
        var taskId = Guid.NewGuid();
        var caseId = Guid.NewGuid();

        await _handler.HandleAsync(new TaskCreated
        {
            TaskId = taskId,
            CaseId = caseId,
            Title = "Follow up",
            Priority = TaskPriority.High,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await _handler.HandleAsync(new TaskCompleted
        {
            TaskId = taskId,
            CaseId = caseId,
            CompletedBy = "coordinator",
            CompletedAt = DateTimeOffset.UtcNow
        });

        var summary = _store.GetDashboardSummary();
        Assert.Equal(0, summary.OpenTaskCount);
    }

    [Fact]
    public async Task AppointmentBookedAndCompleted_UpdatesPendingCount()
    {
        var apptId = Guid.NewGuid();
        var caseId = Guid.NewGuid();

        await _handler.HandleAsync(new AppointmentBooked
        {
            AppointmentId = apptId,
            CaseId = caseId,
            Type = "FollowUp",
            ScheduledAt = DateTimeOffset.UtcNow.AddDays(3)
        });

        Assert.Equal(1, _store.GetDashboardSummary().PendingAppointmentCount);

        await _handler.HandleAsync(new AppointmentCompleted
        {
            AppointmentId = apptId,
            CaseId = caseId,
            CompletedAt = DateTimeOffset.UtcNow
        });

        Assert.Equal(0, _store.GetDashboardSummary().PendingAppointmentCount);
    }

    [Fact]
    public async Task DuplicateEvent_IsSkipped()
    {
        var eventId = Guid.NewGuid();
        var @event = new CaseCreated
        {
            EventId = eventId,
            CaseId = Guid.NewGuid(),
            PatientId = "P001",
            PatientName = "John Doe",
            DiagnosisCode = "I50.9",
            DiagnosisDescription = "Heart failure",
            DischargeDate = DateTimeOffset.UtcNow,
            Status = CaseStatus.Active
        };

        await _handler.HandleAsync(@event);
        await _handler.HandleAsync(@event);

        var summary = _store.GetDashboardSummary();
        Assert.Equal(1, summary.ActiveCaseCount);
    }

    [Fact]
    public async Task RecentCases_CappedAt10()
    {
        for (int i = 0; i < 15; i++)
        {
            await _handler.HandleAsync(new CaseCreated
            {
                CaseId = Guid.NewGuid(),
                PatientId = $"P{i:D3}",
                PatientName = $"Patient {i}",
                DiagnosisCode = "I50.9",
                DiagnosisDescription = "Heart failure",
                DischargeDate = DateTimeOffset.UtcNow,
                Status = CaseStatus.Active
            });
        }

        var summary = _store.GetDashboardSummary();
        Assert.Equal(10, summary.RecentCases.Count);
        Assert.Equal("Patient 14", summary.RecentCases[0].PatientName);
    }

    [Fact]
    public async Task TopAlerts_SortedBySeverityThenAge()
    {
        var now = DateTimeOffset.UtcNow;

        await _handler.HandleAsync(new AlertRaised
        {
            AlertId = Guid.NewGuid(),
            CaseId = Guid.NewGuid(),
            AlertType = AlertType.AbnormalReading,
            Severity = Severity.Medium,
            Title = "Medium",
            Description = "Test",
            CreatedAt = now.AddMinutes(-5)
        });

        await _handler.HandleAsync(new AlertRaised
        {
            AlertId = Guid.NewGuid(),
            CaseId = Guid.NewGuid(),
            AlertType = AlertType.AbnormalReading,
            Severity = Severity.Critical,
            Title = "Critical",
            Description = "Test",
            CreatedAt = now
        });

        var summary = _store.GetDashboardSummary();
        Assert.Equal(2, summary.TopAlerts.Count);
        Assert.Equal("Critical", summary.TopAlerts[0].Severity);
        Assert.Equal("Medium", summary.TopAlerts[1].Severity);
    }

    [Fact]
    public async Task CountsNeverGoBelowZero()
    {
        await _handler.HandleAsync(new TaskCompleted
        {
            TaskId = Guid.NewGuid(),
            CaseId = Guid.NewGuid(),
            CompletedBy = "user",
            CompletedAt = DateTimeOffset.UtcNow
        });

        var summary = _store.GetDashboardSummary();
        Assert.Equal(0, summary.OpenTaskCount);
    }
}
