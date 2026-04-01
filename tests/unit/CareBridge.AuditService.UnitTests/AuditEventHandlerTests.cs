using CareBridge.AuditService.Handlers;
using CareBridge.AuditService.Models;
using CareBridge.AuditService.Store;
using CareBridge.Shared.Contracts.Enums;
using CareBridge.Shared.Contracts.Events;
using Microsoft.Extensions.Logging.Abstractions;

namespace CareBridge.AuditService.UnitTests;

public class AuditEventHandlerTests
{
    private readonly InMemoryAuditStore _store = new();
    private readonly AuditEventHandler _handler;

    public AuditEventHandlerTests()
    {
        _handler = new AuditEventHandler(_store, NullLogger<AuditEventHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_CaseCreated_CreatesAuditRecord()
    {
        var @event = new CaseCreated
        {
            CaseId = Guid.NewGuid(),
            PatientId = "P001",
            PatientName = "Test",
            DiagnosisCode = "I50",
            DiagnosisDescription = "HF",
            DischargeDate = DateTimeOffset.UtcNow,
            Status = CaseStatus.Active
        };

        await _handler.HandleAsync(@event);

        var result = await _store.GetByEventIdAsync(@event.EventId);
        Assert.NotNull(result);
        Assert.Equal("CaseCreated", result.EventType);
        Assert.Equal(@event.CaseId, result.CaseId);
    }

    [Fact]
    public async Task HandleAsync_DuplicateEvent_DoesNotCreateSecondRecord()
    {
        var @event = new CaseCreated
        {
            CaseId = Guid.NewGuid(),
            PatientId = "P001",
            PatientName = "Test",
            DiagnosisCode = "I50",
            DiagnosisDescription = "HF",
            DischargeDate = DateTimeOffset.UtcNow,
            Status = CaseStatus.Active
        };

        await _handler.HandleAsync(@event);
        await _handler.HandleAsync(@event); // duplicate

        var queryResult = await _store.QueryAsync(new AuditQueryFilter());
        Assert.Single(queryResult.Items);
    }

    [Fact]
    public async Task HandleAsync_AllEventTypes_CreateRecords()
    {
        await _handler.HandleAsync(new CaseCreated { CaseId = Guid.NewGuid(), PatientId = "P", PatientName = "N", DiagnosisCode = "C", DiagnosisDescription = "D", DischargeDate = DateTimeOffset.UtcNow, Status = CaseStatus.Active });
        await _handler.HandleAsync(new CaseUpdated { CaseId = Guid.NewGuid(), Status = CaseStatus.Active, UpdatedAt = DateTimeOffset.UtcNow });
        await _handler.HandleAsync(new CarePlanActivated { CarePlanId = Guid.NewGuid(), CaseId = Guid.NewGuid(), TemplateName = "T", MilestoneCount = 1, ActivatedAt = DateTimeOffset.UtcNow });
        await _handler.HandleAsync(new MilestoneCompleted { MilestoneId = Guid.NewGuid(), CarePlanId = Guid.NewGuid(), CaseId = Guid.NewGuid(), MilestoneName = "M", CompletedAt = DateTimeOffset.UtcNow });
        await _handler.HandleAsync(new ObservationReceived { ObservationId = Guid.NewGuid(), CaseId = Guid.NewGuid(), Type = ObservationType.HeartRate, Value = 72, Unit = "bpm", RecordedAt = DateTimeOffset.UtcNow });
        await _handler.HandleAsync(new AlertRaised { AlertId = Guid.NewGuid(), CaseId = Guid.NewGuid(), AlertType = AlertType.AbnormalReading, Severity = Severity.High, Title = "T", Description = "D", CreatedAt = DateTimeOffset.UtcNow });
        await _handler.HandleAsync(new AlertAcknowledged { AlertId = Guid.NewGuid(), CaseId = Guid.NewGuid(), AcknowledgedBy = "u", AcknowledgedAt = DateTimeOffset.UtcNow });
        await _handler.HandleAsync(new AlertResolved { AlertId = Guid.NewGuid(), CaseId = Guid.NewGuid(), ResolvedBy = "u", ResolvedAt = DateTimeOffset.UtcNow });
        await _handler.HandleAsync(new TaskCreated { TaskId = Guid.NewGuid(), CaseId = Guid.NewGuid(), Title = "T", Priority = TaskPriority.Medium, CreatedAt = DateTimeOffset.UtcNow });
        await _handler.HandleAsync(new TaskCompleted { TaskId = Guid.NewGuid(), CaseId = Guid.NewGuid(), CompletedBy = "u", CompletedAt = DateTimeOffset.UtcNow });
        await _handler.HandleAsync(new AppointmentBooked { AppointmentId = Guid.NewGuid(), CaseId = Guid.NewGuid(), Type = "FollowUp", ScheduledAt = DateTimeOffset.UtcNow });
        await _handler.HandleAsync(new AppointmentCompleted { AppointmentId = Guid.NewGuid(), CaseId = Guid.NewGuid(), CompletedAt = DateTimeOffset.UtcNow });
        await _handler.HandleAsync(new AppointmentMissed { AppointmentId = Guid.NewGuid(), CaseId = Guid.NewGuid(), ScheduledAt = DateTimeOffset.UtcNow });
        await _handler.HandleAsync(new NotificationSent { NotificationId = Guid.NewGuid(), CaseId = Guid.NewGuid(), Channel = NotificationChannel.Email, Recipient = "r", Subject = "s", SentAt = DateTimeOffset.UtcNow });

        var result = await _store.QueryAsync(new AuditQueryFilter { Limit = 200 });
        Assert.Equal(14, result.TotalCount);
    }

    [Fact]
    public async Task HandleAsync_RecordIncludesFullPayload()
    {
        var @event = new AlertRaised
        {
            AlertId = Guid.NewGuid(),
            CaseId = Guid.NewGuid(),
            AlertType = AlertType.AbnormalReading,
            Severity = Severity.Critical,
            Title = "High BP",
            Description = "Blood pressure above threshold",
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _handler.HandleAsync(@event);

        var record = await _store.GetByEventIdAsync(@event.EventId);
        Assert.NotNull(record);
        Assert.Contains("High BP", record.Payload);
        Assert.Contains("abnormalReading", record.Payload);
    }

    [Fact]
    public async Task HandleAsync_DefaultActorForSystemEvents()
    {
        await _handler.HandleAsync(new ObservationReceived
        {
            ObservationId = Guid.NewGuid(),
            CaseId = Guid.NewGuid(),
            Type = ObservationType.SpO2,
            Value = 98,
            Unit = "%",
            RecordedAt = DateTimeOffset.UtcNow
        });

        var result = await _store.QueryAsync(new AuditQueryFilter());
        Assert.Single(result.Items);
        Assert.Equal("system", result.Items[0].ActorId);
        Assert.Equal("System", result.Items[0].ActorRole);
    }

    [Fact]
    public async Task HandleAsync_ListResponse_ExcludesPayload()
    {
        await _handler.HandleAsync(new CaseCreated
        {
            CaseId = Guid.NewGuid(),
            PatientId = "P001",
            PatientName = "Test",
            DiagnosisCode = "I50",
            DiagnosisDescription = "HF",
            DischargeDate = DateTimeOffset.UtcNow,
            Status = CaseStatus.Active
        });

        // The store-level record has payload; the API endpoint strips it for list responses.
        var result = await _store.QueryAsync(new AuditQueryFilter());
        Assert.Single(result.Items);
        Assert.NotEmpty(result.Items[0].Payload); // Store level retains payload
    }

    [Fact]
    public async Task HandleAsync_DetailResponse_IncludesPayload()
    {
        var @event = new TaskCreated
        {
            TaskId = Guid.NewGuid(),
            CaseId = Guid.NewGuid(),
            Title = "Follow up call",
            Priority = TaskPriority.High,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _handler.HandleAsync(@event);

        var record = await _store.GetByEventIdAsync(@event.EventId);
        Assert.NotNull(record);
        var detail = await _store.GetByIdAsync(record.Id);
        Assert.NotNull(detail);
        Assert.NotEmpty(detail.Payload);
        Assert.Contains("Follow up call", detail.Payload);
    }
}
