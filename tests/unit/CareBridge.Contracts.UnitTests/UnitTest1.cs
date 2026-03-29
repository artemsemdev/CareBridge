using System.Text.Json;
using CareBridge.Shared.Contracts.Enums;
using CareBridge.Shared.Contracts.Events;
using CareBridge.Shared.Contracts.Serialization;

namespace CareBridge.Contracts.UnitTests;

public class EventSerializationTests
{
    [Fact]
    public void CaseCreated_RoundTrips()
    {
        var evt = new CaseCreated
        {
            CaseId = Guid.NewGuid(),
            PatientId = "P-" + Guid.NewGuid().ToString("N")[..8],
            PatientName = "John Doe",
            DiagnosisCode = "I50.9",
            DiagnosisDescription = "Heart failure, unspecified",
            DischargeDate = DateTimeOffset.UtcNow,
            Status = CaseStatus.Active
        };
        AssertRoundTrip(evt);
    }

    [Fact]
    public void CaseUpdated_RoundTrips()
    {
        var evt = new CaseUpdated
        {
            CaseId = Guid.NewGuid(),
            Status = CaseStatus.Monitoring,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        AssertRoundTrip(evt);
    }

    [Fact]
    public void CarePlanActivated_RoundTrips()
    {
        var evt = new CarePlanActivated
        {
            CarePlanId = Guid.NewGuid(),
            CaseId = Guid.NewGuid(),
            TemplateName = "General Post-Discharge",
            MilestoneCount = 5,
            ActivatedAt = DateTimeOffset.UtcNow
        };
        AssertRoundTrip(evt);
    }

    [Fact]
    public void MilestoneCompleted_RoundTrips()
    {
        var evt = new MilestoneCompleted
        {
            MilestoneId = Guid.NewGuid(),
            CarePlanId = Guid.NewGuid(),
            CaseId = Guid.NewGuid(),
            MilestoneName = "Initial outreach",
            CompletedAt = DateTimeOffset.UtcNow
        };
        AssertRoundTrip(evt);
    }

    [Fact]
    public void ObservationReceived_RoundTrips()
    {
        var evt = new ObservationReceived
        {
            ObservationId = Guid.NewGuid(),
            CaseId = Guid.NewGuid(),
            Type = ObservationType.BloodPressure,
            Value = 120.5m,
            Unit = "mmHg",
            RecordedAt = DateTimeOffset.UtcNow
        };
        AssertRoundTrip(evt);
    }

    [Fact]
    public void AlertRaised_RoundTrips()
    {
        var evt = new AlertRaised
        {
            AlertId = Guid.NewGuid(),
            CaseId = Guid.NewGuid(),
            AlertType = AlertType.AbnormalReading,
            Severity = Severity.High,
            Description = "Blood pressure critically elevated",
            CreatedAt = DateTimeOffset.UtcNow
        };
        AssertRoundTrip(evt);
    }

    [Fact]
    public void AlertAcknowledged_RoundTrips()
    {
        var evt = new AlertAcknowledged
        {
            AlertId = Guid.NewGuid(),
            CaseId = Guid.NewGuid(),
            AcknowledgedBy = "nurse@example.com",
            AcknowledgedAt = DateTimeOffset.UtcNow
        };
        AssertRoundTrip(evt);
    }

    [Fact]
    public void AlertResolved_RoundTrips()
    {
        var evt = new AlertResolved
        {
            AlertId = Guid.NewGuid(),
            CaseId = Guid.NewGuid(),
            ResolvedBy = "doctor@example.com",
            ResolvedAt = DateTimeOffset.UtcNow
        };
        AssertRoundTrip(evt);
    }

    [Fact]
    public void TaskCreated_RoundTrips()
    {
        var evt = new TaskCreated
        {
            TaskId = Guid.NewGuid(),
            CaseId = Guid.NewGuid(),
            AlertId = Guid.NewGuid(),
            Title = "Follow up on abnormal reading",
            Priority = TaskPriority.High,
            CreatedAt = DateTimeOffset.UtcNow
        };
        AssertRoundTrip(evt);
    }

    [Fact]
    public void TaskCompleted_RoundTrips()
    {
        var evt = new TaskCompleted
        {
            TaskId = Guid.NewGuid(),
            CaseId = Guid.NewGuid(),
            CompletedBy = "coordinator@example.com",
            CompletedAt = DateTimeOffset.UtcNow
        };
        AssertRoundTrip(evt);
    }

    [Fact]
    public void AppointmentBooked_RoundTrips()
    {
        var evt = new AppointmentBooked
        {
            AppointmentId = Guid.NewGuid(),
            CaseId = Guid.NewGuid(),
            Type = "Follow-up",
            ScheduledAt = DateTimeOffset.UtcNow.AddDays(7)
        };
        AssertRoundTrip(evt);
    }

    [Fact]
    public void AppointmentCompleted_RoundTrips()
    {
        var evt = new AppointmentCompleted
        {
            AppointmentId = Guid.NewGuid(),
            CaseId = Guid.NewGuid(),
            CompletedAt = DateTimeOffset.UtcNow
        };
        AssertRoundTrip(evt);
    }

    [Fact]
    public void AppointmentMissed_RoundTrips()
    {
        var evt = new AppointmentMissed
        {
            AppointmentId = Guid.NewGuid(),
            CaseId = Guid.NewGuid(),
            ScheduledAt = DateTimeOffset.UtcNow.AddDays(-1)
        };
        AssertRoundTrip(evt);
    }

    [Fact]
    public void NotificationSent_RoundTrips()
    {
        var evt = new NotificationSent
        {
            NotificationId = Guid.NewGuid(),
            CaseId = Guid.NewGuid(),
            Channel = NotificationChannel.Email,
            Recipient = "patient@example.com",
            Subject = "Appointment reminder",
            SentAt = DateTimeOffset.UtcNow
        };
        AssertRoundTrip(evt);
    }

    [Fact]
    public void IntegrationEvent_HasCorrectDefaults()
    {
        var evt = new CaseCreated
        {
            CaseId = Guid.NewGuid(),
            PatientId = "P-" + Guid.NewGuid().ToString("N")[..8],
            PatientName = "Test",
            DiagnosisCode = "J18.9",
            DiagnosisDescription = "Pneumonia",
            DischargeDate = DateTimeOffset.UtcNow,
            Status = CaseStatus.Active
        };

        Assert.NotEqual(Guid.Empty, evt.EventId);
        Assert.True(evt.OccurredAt > DateTimeOffset.MinValue);
        Assert.Equal(1, evt.Version);
        Assert.Contains("CaseCreated", evt.EventType);
    }

    [Fact]
    public void Enums_SerializeAsStrings()
    {
        var evt = new AlertRaised
        {
            AlertId = Guid.NewGuid(),
            CaseId = Guid.NewGuid(),
            AlertType = AlertType.AbnormalReading,
            Severity = Severity.Critical,
            Description = "Test",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var json = JsonSerializer.Serialize(evt, JsonDefaults.Options);
        Assert.Contains("\"abnormalReading\"", json);
        Assert.Contains("\"critical\"", json);
    }

    private static void AssertRoundTrip<T>(T original) where T : IntegrationEvent
    {
        var json = JsonSerializer.Serialize(original, JsonDefaults.Options);
        Assert.False(string.IsNullOrWhiteSpace(json));

        var deserialized = JsonSerializer.Deserialize<T>(json, JsonDefaults.Options);
        Assert.NotNull(deserialized);
        Assert.Equal(original.EventId, deserialized.EventId);
        Assert.Equal(original.Version, deserialized.Version);
    }
}
