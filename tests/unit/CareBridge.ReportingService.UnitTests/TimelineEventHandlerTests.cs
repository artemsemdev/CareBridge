using CareBridge.ReportingService.Handlers;
using CareBridge.ReportingService.Store;
using CareBridge.Shared.Contracts.Enums;
using CareBridge.Shared.Contracts.Events;
using Microsoft.Extensions.Logging.Abstractions;

namespace CareBridge.ReportingService.UnitTests;

public class TimelineEventHandlerTests
{
    private readonly InMemoryReadModelStore _store = new();
    private readonly TimelineEventHandler _handler;

    public TimelineEventHandlerTests()
    {
        _handler = new TimelineEventHandler(_store, NullLogger<TimelineEventHandler>.Instance);
    }

    [Fact]
    public async Task CaseCreated_AddsTimelineEntry()
    {
        var caseId = Guid.NewGuid();
        await _handler.HandleAsync(new CaseCreated
        {
            CaseId = caseId,
            PatientId = "P001",
            PatientName = "Jane Smith",
            DiagnosisCode = "I50.9",
            DiagnosisDescription = "Heart failure",
            DischargeDate = DateTimeOffset.UtcNow,
            Status = CaseStatus.Active
        });

        var timeline = _store.GetOrCreateTimeline(caseId);
        Assert.Single(timeline.Entries);
        Assert.Equal("CaseCreated", timeline.Entries[0].EventType);
        Assert.Equal("case", timeline.Entries[0].Category);
        Assert.Equal("Case created", timeline.Entries[0].Title);
        Assert.Contains("Heart failure", timeline.Entries[0].Description);
    }

    [Fact]
    public async Task AllEventTypes_CreateCorrectEntries()
    {
        var caseId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await _handler.HandleAsync(new CaseCreated
        {
            CaseId = caseId, PatientId = "P001", PatientName = "Test",
            DiagnosisCode = "I50.9", DiagnosisDescription = "HF",
            DischargeDate = now, Status = CaseStatus.Active
        });
        await _handler.HandleAsync(new CaseUpdated
        {
            CaseId = caseId, Status = CaseStatus.Monitoring, UpdatedAt = now.AddHours(1)
        });
        await _handler.HandleAsync(new CarePlanActivated
        {
            CarePlanId = Guid.NewGuid(), CaseId = caseId,
            TemplateName = "General Post-Discharge", MilestoneCount = 5,
            ActivatedAt = now.AddHours(1)
        });
        await _handler.HandleAsync(new MilestoneCompleted
        {
            MilestoneId = Guid.NewGuid(), CarePlanId = Guid.NewGuid(),
            CaseId = caseId, MilestoneName = "Initial outreach",
            CompletedAt = now.AddDays(2)
        });
        await _handler.HandleAsync(new ObservationReceived
        {
            ObservationId = Guid.NewGuid(), CaseId = caseId,
            Type = ObservationType.BloodPressure, Value = 140, Unit = "mmHg",
            RecordedAt = now.AddDays(1)
        });
        await _handler.HandleAsync(new AlertRaised
        {
            AlertId = Guid.NewGuid(), CaseId = caseId,
            AlertType = AlertType.AbnormalReading, Severity = Severity.High,
            Title = "High BP", Description = "Systolic 190",
            CreatedAt = now.AddDays(1)
        });
        await _handler.HandleAsync(new AlertAcknowledged
        {
            AlertId = Guid.NewGuid(), CaseId = caseId,
            AcknowledgedBy = "coord1", AcknowledgedAt = now.AddDays(1)
        });
        await _handler.HandleAsync(new AlertResolved
        {
            AlertId = Guid.NewGuid(), CaseId = caseId,
            ResolvedBy = "coord1", ResolvedAt = now.AddDays(2)
        });
        await _handler.HandleAsync(new TaskCreated
        {
            TaskId = Guid.NewGuid(), CaseId = caseId,
            Title = "Follow up on BP", Priority = TaskPriority.High,
            CreatedAt = now.AddDays(1)
        });
        await _handler.HandleAsync(new TaskCompleted
        {
            TaskId = Guid.NewGuid(), CaseId = caseId,
            CompletedBy = "coord1", CompletedAt = now.AddDays(2)
        });
        await _handler.HandleAsync(new AppointmentBooked
        {
            AppointmentId = Guid.NewGuid(), CaseId = caseId,
            Type = "FollowUp", ScheduledAt = now.AddDays(7)
        });
        await _handler.HandleAsync(new AppointmentCompleted
        {
            AppointmentId = Guid.NewGuid(), CaseId = caseId,
            CompletedAt = now.AddDays(7)
        });
        await _handler.HandleAsync(new AppointmentMissed
        {
            AppointmentId = Guid.NewGuid(), CaseId = caseId,
            ScheduledAt = now.AddDays(7)
        });
        await _handler.HandleAsync(new NotificationSent
        {
            NotificationId = Guid.NewGuid(), CaseId = caseId,
            Channel = NotificationChannel.Email, Recipient = "patient@test.com",
            Subject = "Appointment reminder", SentAt = now.AddDays(6)
        });

        var timeline = _store.GetOrCreateTimeline(caseId);
        Assert.Equal(14, timeline.Entries.Count);

        var categories = timeline.Entries.Select(e => e.Category).Distinct().OrderBy(c => c).ToList();
        Assert.Contains("case", categories);
        Assert.Contains("careplan", categories);
        Assert.Contains("observation", categories);
        Assert.Contains("alert", categories);
        Assert.Contains("task", categories);
        Assert.Contains("appointment", categories);
        Assert.Contains("notification", categories);
    }

    [Fact]
    public async Task DuplicateEvent_IsSkipped()
    {
        var caseId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var @event = new CaseCreated
        {
            EventId = eventId,
            CaseId = caseId,
            PatientId = "P001",
            PatientName = "Test",
            DiagnosisCode = "I50.9",
            DiagnosisDescription = "HF",
            DischargeDate = DateTimeOffset.UtcNow,
            Status = CaseStatus.Active
        };

        await _handler.HandleAsync(@event);
        await _handler.HandleAsync(@event);

        var timeline = _store.GetOrCreateTimeline(caseId);
        Assert.Single(timeline.Entries);
    }

    [Fact]
    public async Task OutOfOrderEvents_AreSortedByTimestamp()
    {
        var caseId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        // Add events out of order
        await _handler.HandleAsync(new AlertRaised
        {
            AlertId = Guid.NewGuid(), CaseId = caseId,
            AlertType = AlertType.AbnormalReading, Severity = Severity.High,
            Title = "Alert", Description = "Alert desc",
            CreatedAt = now.AddHours(2)
        });

        await _handler.HandleAsync(new CaseCreated
        {
            CaseId = caseId, PatientId = "P001", PatientName = "Test",
            DiagnosisCode = "I50.9", DiagnosisDescription = "HF",
            DischargeDate = now.AddHours(-24),
            Status = CaseStatus.Active
        });

        await _handler.HandleAsync(new ObservationReceived
        {
            ObservationId = Guid.NewGuid(), CaseId = caseId,
            Type = ObservationType.HeartRate, Value = 90, Unit = "bpm",
            RecordedAt = now.AddHours(1)
        });

        var timeline = _store.GetOrCreateTimeline(caseId);

        // Should be sorted newest first
        Assert.Equal("AlertRaised", timeline.Entries[0].EventType);
        Assert.Equal("ObservationReceived", timeline.Entries[1].EventType);
        Assert.Equal("CaseCreated", timeline.Entries[2].EventType);
    }

    [Fact]
    public async Task AlertEntry_IncludesSeverity()
    {
        var caseId = Guid.NewGuid();
        await _handler.HandleAsync(new AlertRaised
        {
            AlertId = Guid.NewGuid(), CaseId = caseId,
            AlertType = AlertType.AbnormalReading, Severity = Severity.Critical,
            Title = "High BP", Description = "Systolic 200",
            CreatedAt = DateTimeOffset.UtcNow
        });

        var timeline = _store.GetOrCreateTimeline(caseId);
        Assert.Equal("Critical", timeline.Entries[0].Severity);
    }

    [Fact]
    public async Task TimelineEntry_HasHumanReadableDescriptions()
    {
        var caseId = Guid.NewGuid();

        await _handler.HandleAsync(new CarePlanActivated
        {
            CarePlanId = Guid.NewGuid(),
            CaseId = caseId,
            TemplateName = "General Post-Discharge",
            MilestoneCount = 5,
            ActivatedAt = DateTimeOffset.UtcNow
        });

        var timeline = _store.GetOrCreateTimeline(caseId);
        Assert.Equal("Care plan activated", timeline.Entries[0].Title);
        Assert.Equal("General Post-Discharge with 5 milestones", timeline.Entries[0].Description);
    }

    [Fact]
    public async Task ActorField_PopulatedFromEvent()
    {
        var caseId = Guid.NewGuid();

        await _handler.HandleAsync(new AlertAcknowledged
        {
            AlertId = Guid.NewGuid(),
            CaseId = caseId,
            AcknowledgedBy = "nurse-jane",
            AcknowledgedAt = DateTimeOffset.UtcNow
        });

        var timeline = _store.GetOrCreateTimeline(caseId);
        Assert.Equal("nurse-jane", timeline.Entries[0].Actor);
    }
}
