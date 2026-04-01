using System.Text.Json;
using CareBridge.AuditService.Models;
using CareBridge.Shared.Contracts.Events;
using CareBridge.Shared.Contracts.Serialization;

namespace CareBridge.AuditService.Handlers;

// Audit: Centralized mapping from domain events to immutable audit records.
// Each event type maps to a human-readable action, entity type, and actor extraction.
public static class AuditEventMapper
{
    private static readonly Dictionary<Type, EventMapping> Mappings = new()
    {
        [typeof(CaseCreated)] = new("CaseCreated", "Created case", "Case", "CaseService"),
        [typeof(CaseUpdated)] = new("CaseUpdated", "Updated case status", "Case", "CaseService"),
        [typeof(CarePlanActivated)] = new("CarePlanActivated", "Activated care plan", "CarePlan", "CarePlanService"),
        [typeof(MilestoneCompleted)] = new("MilestoneCompleted", "Completed milestone", "Milestone", "CarePlanService"),
        [typeof(ObservationReceived)] = new("ObservationReceived", "Recorded observation", "Observation", "ObservationService"),
        [typeof(AlertRaised)] = new("AlertRaised", "Raised alert", "Alert", "CareGapEngine"),
        [typeof(AlertAcknowledged)] = new("AlertAcknowledged", "Acknowledged alert", "Alert", "CareGapEngine"),
        [typeof(AlertResolved)] = new("AlertResolved", "Resolved alert", "Alert", "CareGapEngine"),
        [typeof(TaskCreated)] = new("TaskCreated", "Created task", "Task", "TaskService"),
        [typeof(TaskCompleted)] = new("TaskCompleted", "Completed task", "Task", "TaskService"),
        [typeof(AppointmentBooked)] = new("AppointmentBooked", "Booked appointment", "Appointment", "AppointmentService"),
        [typeof(AppointmentCompleted)] = new("AppointmentCompleted", "Completed appointment", "Appointment", "AppointmentService"),
        [typeof(AppointmentMissed)] = new("AppointmentMissed", "Missed appointment", "Appointment", "AppointmentService"),
        [typeof(NotificationSent)] = new("NotificationSent", "Sent notification", "Notification", "NotificationService"),
    };

    public static AuditEvent Map(IntegrationEvent @event)
    {
        var eventType = @event.GetType();

        if (!Mappings.TryGetValue(eventType, out var mapping))
            throw new InvalidOperationException($"No audit mapping for event type {eventType.Name}");

        var (actorId, actorRole) = ExtractActor(@event);
        var entityId = ExtractEntityId(@event);
        var caseId = ExtractCaseId(@event);
        var payload = JsonSerializer.Serialize(@event, eventType, JsonDefaults.Options);

        return new AuditEvent
        {
            Id = Guid.NewGuid(),
            Timestamp = @event.OccurredAt,
            CorrelationId = @event.CorrelationId,
            EventType = mapping.EventType,
            Action = mapping.Action,
            ActorId = actorId,
            ActorRole = actorRole,
            EntityType = mapping.EntityType,
            EntityId = entityId,
            CaseId = caseId,
            ServiceSource = mapping.ServiceSource,
            Payload = payload,
            EventId = @event.EventId
        };
    }

    private static (string ActorId, string ActorRole) ExtractActor(IntegrationEvent @event) => @event switch
    {
        AlertAcknowledged e => (e.AcknowledgedBy, "CareCoordinator"),
        AlertResolved e => (e.ResolvedBy, "CareCoordinator"),
        TaskCompleted e => (e.CompletedBy, "CareCoordinator"),
        AppointmentBooked => ("system", "System"),
        AppointmentCompleted => ("system", "System"),
        ObservationReceived => ("system", "System"),
        AlertRaised => ("system", "System"),
        AppointmentMissed => ("system", "System"),
        CarePlanActivated => ("system", "System"),
        NotificationSent => ("system", "System"),
        TaskCreated => ("system", "System"),
        CaseCreated => ("system", "System"),
        CaseUpdated => ("system", "System"),
        MilestoneCompleted => ("system", "System"),
        _ => ("system", "System")
    };

    private static Guid ExtractEntityId(IntegrationEvent @event) => @event switch
    {
        CaseCreated e => e.CaseId,
        CaseUpdated e => e.CaseId,
        CarePlanActivated e => e.CarePlanId,
        MilestoneCompleted e => e.MilestoneId,
        ObservationReceived e => e.ObservationId,
        AlertRaised e => e.AlertId,
        AlertAcknowledged e => e.AlertId,
        AlertResolved e => e.AlertId,
        TaskCreated e => e.TaskId,
        TaskCompleted e => e.TaskId,
        AppointmentBooked e => e.AppointmentId,
        AppointmentCompleted e => e.AppointmentId,
        AppointmentMissed e => e.AppointmentId,
        NotificationSent e => e.NotificationId,
        _ => Guid.Empty
    };

    private static Guid ExtractCaseId(IntegrationEvent @event) => @event switch
    {
        CaseCreated e => e.CaseId,
        CaseUpdated e => e.CaseId,
        CarePlanActivated e => e.CaseId,
        MilestoneCompleted e => e.CaseId,
        ObservationReceived e => e.CaseId,
        AlertRaised e => e.CaseId,
        AlertAcknowledged e => e.CaseId,
        AlertResolved e => e.CaseId,
        TaskCreated e => e.CaseId,
        TaskCompleted e => e.CaseId,
        AppointmentBooked e => e.CaseId,
        AppointmentCompleted e => e.CaseId,
        AppointmentMissed e => e.CaseId,
        NotificationSent e => e.CaseId,
        _ => Guid.Empty
    };

    private record EventMapping(string EventType, string Action, string EntityType, string ServiceSource);
}
