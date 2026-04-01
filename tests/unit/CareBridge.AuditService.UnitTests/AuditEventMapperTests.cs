using CareBridge.AuditService.Handlers;
using CareBridge.Shared.Contracts.Enums;
using CareBridge.Shared.Contracts.Events;

namespace CareBridge.AuditService.UnitTests;

public class AuditEventMapperTests
{
    [Fact]
    public void Map_CaseCreated_MapsCorrectly()
    {
        var caseId = Guid.NewGuid();
        var @event = new CaseCreated
        {
            CaseId = caseId,
            PatientId = "P001",
            PatientName = "Test Patient",
            DiagnosisCode = "I50.9",
            DiagnosisDescription = "Heart failure",
            DischargeDate = DateTimeOffset.UtcNow,
            Status = CaseStatus.Active
        };

        var result = AuditEventMapper.Map(@event);

        Assert.Equal("CaseCreated", result.EventType);
        Assert.Equal("Created case", result.Action);
        Assert.Equal("Case", result.EntityType);
        Assert.Equal("CaseService", result.ServiceSource);
        Assert.Equal(caseId, result.EntityId);
        Assert.Equal(caseId, result.CaseId);
        Assert.Equal("system", result.ActorId);
        Assert.Equal("System", result.ActorRole);
        Assert.Contains("caseId", result.Payload);
    }

    [Fact]
    public void Map_CaseUpdated_MapsCorrectly()
    {
        var caseId = Guid.NewGuid();
        var result = AuditEventMapper.Map(new CaseUpdated
        {
            CaseId = caseId,
            Status = CaseStatus.Monitoring,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        Assert.Equal("CaseUpdated", result.EventType);
        Assert.Equal("Updated case status", result.Action);
        Assert.Equal("Case", result.EntityType);
        Assert.Equal(caseId, result.CaseId);
    }

    [Fact]
    public void Map_AlertRaised_UsesSystemActor()
    {
        var result = AuditEventMapper.Map(new AlertRaised
        {
            AlertId = Guid.NewGuid(),
            CaseId = Guid.NewGuid(),
            AlertType = AlertType.AbnormalReading,
            Severity = Severity.Critical,
            Title = "High BP",
            Description = "Blood pressure above threshold",
            CreatedAt = DateTimeOffset.UtcNow
        });

        Assert.Equal("AlertRaised", result.EventType);
        Assert.Equal("Raised alert", result.Action);
        Assert.Equal("Alert", result.EntityType);
        Assert.Equal("CareGapEngine", result.ServiceSource);
        Assert.Equal("system", result.ActorId);
    }

    [Fact]
    public void Map_AlertAcknowledged_ExtractsActor()
    {
        var result = AuditEventMapper.Map(new AlertAcknowledged
        {
            AlertId = Guid.NewGuid(),
            CaseId = Guid.NewGuid(),
            AcknowledgedBy = "care-coordinator",
            AcknowledgedAt = DateTimeOffset.UtcNow
        });

        Assert.Equal("AlertAcknowledged", result.EventType);
        Assert.Equal("care-coordinator", result.ActorId);
        Assert.Equal("CareCoordinator", result.ActorRole);
    }

    [Fact]
    public void Map_AlertResolved_ExtractsActor()
    {
        var result = AuditEventMapper.Map(new AlertResolved
        {
            AlertId = Guid.NewGuid(),
            CaseId = Guid.NewGuid(),
            ResolvedBy = "dr-smith",
            ResolvedAt = DateTimeOffset.UtcNow
        });

        Assert.Equal("AlertResolved", result.EventType);
        Assert.Equal("dr-smith", result.ActorId);
    }

    [Fact]
    public void Map_TaskCreated_MapsCorrectly()
    {
        var taskId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var result = AuditEventMapper.Map(new TaskCreated
        {
            TaskId = taskId,
            CaseId = caseId,
            Title = "Follow up call",
            Priority = TaskPriority.High,
            CreatedAt = DateTimeOffset.UtcNow
        });

        Assert.Equal("TaskCreated", result.EventType);
        Assert.Equal("Created task", result.Action);
        Assert.Equal("Task", result.EntityType);
        Assert.Equal(taskId, result.EntityId);
        Assert.Equal(caseId, result.CaseId);
    }

    [Fact]
    public void Map_TaskCompleted_ExtractsActor()
    {
        var result = AuditEventMapper.Map(new TaskCompleted
        {
            TaskId = Guid.NewGuid(),
            CaseId = Guid.NewGuid(),
            CompletedBy = "nurse-jane",
            CompletedAt = DateTimeOffset.UtcNow
        });

        Assert.Equal("TaskCompleted", result.EventType);
        Assert.Equal("nurse-jane", result.ActorId);
    }

    [Fact]
    public void Map_CarePlanActivated_UsesSystemActor()
    {
        var planId = Guid.NewGuid();
        var result = AuditEventMapper.Map(new CarePlanActivated
        {
            CarePlanId = planId,
            CaseId = Guid.NewGuid(),
            TemplateName = "CHF Protocol",
            MilestoneCount = 5,
            ActivatedAt = DateTimeOffset.UtcNow
        });

        Assert.Equal("CarePlanActivated", result.EventType);
        Assert.Equal("Activated care plan", result.Action);
        Assert.Equal("CarePlan", result.EntityType);
        Assert.Equal(planId, result.EntityId);
        Assert.Equal("system", result.ActorId);
    }

    [Fact]
    public void Map_MilestoneCompleted_MapsCorrectly()
    {
        var milestoneId = Guid.NewGuid();
        var result = AuditEventMapper.Map(new MilestoneCompleted
        {
            MilestoneId = milestoneId,
            CarePlanId = Guid.NewGuid(),
            CaseId = Guid.NewGuid(),
            MilestoneName = "Initial check-in",
            CompletedAt = DateTimeOffset.UtcNow
        });

        Assert.Equal("MilestoneCompleted", result.EventType);
        Assert.Equal("Milestone", result.EntityType);
        Assert.Equal(milestoneId, result.EntityId);
    }

    [Fact]
    public void Map_ObservationReceived_MapsCorrectly()
    {
        var obsId = Guid.NewGuid();
        var result = AuditEventMapper.Map(new ObservationReceived
        {
            ObservationId = obsId,
            CaseId = Guid.NewGuid(),
            Type = ObservationType.BloodPressure,
            Value = 140,
            Unit = "mmHg",
            RecordedAt = DateTimeOffset.UtcNow
        });

        Assert.Equal("ObservationReceived", result.EventType);
        Assert.Equal("Recorded observation", result.Action);
        Assert.Equal("Observation", result.EntityType);
        Assert.Equal(obsId, result.EntityId);
        Assert.Equal("system", result.ActorId);
    }

    [Fact]
    public void Map_AppointmentBooked_MapsCorrectly()
    {
        var apptId = Guid.NewGuid();
        var result = AuditEventMapper.Map(new AppointmentBooked
        {
            AppointmentId = apptId,
            CaseId = Guid.NewGuid(),
            Type = "FollowUp",
            ScheduledAt = DateTimeOffset.UtcNow.AddDays(7)
        });

        Assert.Equal("AppointmentBooked", result.EventType);
        Assert.Equal("Booked appointment", result.Action);
        Assert.Equal("Appointment", result.EntityType);
        Assert.Equal(apptId, result.EntityId);
    }

    [Fact]
    public void Map_AppointmentCompleted_MapsCorrectly()
    {
        var result = AuditEventMapper.Map(new AppointmentCompleted
        {
            AppointmentId = Guid.NewGuid(),
            CaseId = Guid.NewGuid(),
            CompletedAt = DateTimeOffset.UtcNow
        });

        Assert.Equal("AppointmentCompleted", result.EventType);
        Assert.Equal("Completed appointment", result.Action);
    }

    [Fact]
    public void Map_AppointmentMissed_UsesSystemActor()
    {
        var result = AuditEventMapper.Map(new AppointmentMissed
        {
            AppointmentId = Guid.NewGuid(),
            CaseId = Guid.NewGuid(),
            ScheduledAt = DateTimeOffset.UtcNow.AddDays(-1)
        });

        Assert.Equal("AppointmentMissed", result.EventType);
        Assert.Equal("Missed appointment", result.Action);
        Assert.Equal("system", result.ActorId);
    }

    [Fact]
    public void Map_NotificationSent_MapsCorrectly()
    {
        var notifId = Guid.NewGuid();
        var result = AuditEventMapper.Map(new NotificationSent
        {
            NotificationId = notifId,
            CaseId = Guid.NewGuid(),
            Channel = NotificationChannel.Email,
            Recipient = "patient@example.com",
            Subject = "Follow-up reminder",
            SentAt = DateTimeOffset.UtcNow
        });

        Assert.Equal("NotificationSent", result.EventType);
        Assert.Equal("Sent notification", result.Action);
        Assert.Equal("Notification", result.EntityType);
        Assert.Equal(notifId, result.EntityId);
        Assert.Equal("system", result.ActorId);
    }

    [Fact]
    public void Map_PreservesCorrelationId()
    {
        var correlationId = "test-correlation-123";
        var @event = new CaseCreated
        {
            CorrelationId = correlationId,
            CaseId = Guid.NewGuid(),
            PatientId = "P001",
            PatientName = "Test",
            DiagnosisCode = "I50",
            DiagnosisDescription = "HF",
            DischargeDate = DateTimeOffset.UtcNow,
            Status = CaseStatus.Active
        };

        var result = AuditEventMapper.Map(@event);
        Assert.Equal(correlationId, result.CorrelationId);
    }

    [Fact]
    public void Map_PreservesTimestamp()
    {
        var timestamp = new DateTimeOffset(2026, 3, 15, 10, 30, 0, TimeSpan.Zero);
        var @event = new CaseCreated
        {
            OccurredAt = timestamp,
            CaseId = Guid.NewGuid(),
            PatientId = "P001",
            PatientName = "Test",
            DiagnosisCode = "I50",
            DiagnosisDescription = "HF",
            DischargeDate = DateTimeOffset.UtcNow,
            Status = CaseStatus.Active
        };

        var result = AuditEventMapper.Map(@event);
        Assert.Equal(timestamp, result.Timestamp);
    }

    [Fact]
    public void Map_SerializesFullPayload()
    {
        var @event = new CaseCreated
        {
            CaseId = Guid.NewGuid(),
            PatientId = "P001",
            PatientName = "Jane Doe",
            DiagnosisCode = "I50.9",
            DiagnosisDescription = "Heart failure",
            DischargeDate = DateTimeOffset.UtcNow,
            Status = CaseStatus.Active
        };

        var result = AuditEventMapper.Map(@event);

        Assert.Contains("P001", result.Payload);
        Assert.Contains("Jane Doe", result.Payload);
        Assert.Contains("I50.9", result.Payload);
    }

    [Fact]
    public void Map_EventType_UsesShortForm()
    {
        var @event = new AlertRaised
        {
            AlertId = Guid.NewGuid(),
            CaseId = Guid.NewGuid(),
            AlertType = AlertType.AbnormalReading,
            Severity = Severity.High,
            Title = "Test",
            Description = "Test",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var result = AuditEventMapper.Map(@event);

        // Must be short form, not full CLR type name
        Assert.Equal("AlertRaised", result.EventType);
        Assert.DoesNotContain(".", result.EventType);
        Assert.DoesNotContain("CareBridge", result.EventType);
    }

    [Fact]
    public void Map_All14EventTypes_HaveMappings()
    {
        var events = new IntegrationEvent[]
        {
            new CaseCreated { CaseId = Guid.NewGuid(), PatientId = "P", PatientName = "N", DiagnosisCode = "C", DiagnosisDescription = "D", DischargeDate = DateTimeOffset.UtcNow, Status = CaseStatus.Active },
            new CaseUpdated { CaseId = Guid.NewGuid(), Status = CaseStatus.Active, UpdatedAt = DateTimeOffset.UtcNow },
            new CarePlanActivated { CarePlanId = Guid.NewGuid(), CaseId = Guid.NewGuid(), TemplateName = "T", MilestoneCount = 1, ActivatedAt = DateTimeOffset.UtcNow },
            new MilestoneCompleted { MilestoneId = Guid.NewGuid(), CarePlanId = Guid.NewGuid(), CaseId = Guid.NewGuid(), MilestoneName = "M", CompletedAt = DateTimeOffset.UtcNow },
            new ObservationReceived { ObservationId = Guid.NewGuid(), CaseId = Guid.NewGuid(), Type = ObservationType.HeartRate, Value = 72, Unit = "bpm", RecordedAt = DateTimeOffset.UtcNow },
            new AlertRaised { AlertId = Guid.NewGuid(), CaseId = Guid.NewGuid(), AlertType = AlertType.AbnormalReading, Severity = Severity.High, Title = "T", Description = "D", CreatedAt = DateTimeOffset.UtcNow },
            new AlertAcknowledged { AlertId = Guid.NewGuid(), CaseId = Guid.NewGuid(), AcknowledgedBy = "u", AcknowledgedAt = DateTimeOffset.UtcNow },
            new AlertResolved { AlertId = Guid.NewGuid(), CaseId = Guid.NewGuid(), ResolvedBy = "u", ResolvedAt = DateTimeOffset.UtcNow },
            new TaskCreated { TaskId = Guid.NewGuid(), CaseId = Guid.NewGuid(), Title = "T", Priority = TaskPriority.Medium, CreatedAt = DateTimeOffset.UtcNow },
            new TaskCompleted { TaskId = Guid.NewGuid(), CaseId = Guid.NewGuid(), CompletedBy = "u", CompletedAt = DateTimeOffset.UtcNow },
            new AppointmentBooked { AppointmentId = Guid.NewGuid(), CaseId = Guid.NewGuid(), Type = "FollowUp", ScheduledAt = DateTimeOffset.UtcNow },
            new AppointmentCompleted { AppointmentId = Guid.NewGuid(), CaseId = Guid.NewGuid(), CompletedAt = DateTimeOffset.UtcNow },
            new AppointmentMissed { AppointmentId = Guid.NewGuid(), CaseId = Guid.NewGuid(), ScheduledAt = DateTimeOffset.UtcNow },
            new NotificationSent { NotificationId = Guid.NewGuid(), CaseId = Guid.NewGuid(), Channel = NotificationChannel.Email, Recipient = "r", Subject = "s", SentAt = DateTimeOffset.UtcNow }
        };

        foreach (var @event in events)
        {
            var record = AuditEventMapper.Map(@event);
            Assert.NotEmpty(record.EventType);
            Assert.NotEmpty(record.Action);
            Assert.NotEmpty(record.EntityType);
            Assert.NotEmpty(record.ServiceSource);
            Assert.NotEqual(Guid.Empty, record.EntityId);
            Assert.NotEqual(Guid.Empty, record.CaseId);
        }
    }
}
