namespace CareBridge.Shared.Contracts.Events;

public sealed record AppointmentBooked : IntegrationEvent
{
    public required Guid AppointmentId { get; init; }
    public required Guid CaseId { get; init; }
    public required string Type { get; init; }
    public required DateTimeOffset ScheduledAt { get; init; }
}

public sealed record AppointmentCompleted : IntegrationEvent
{
    public required Guid AppointmentId { get; init; }
    public required Guid CaseId { get; init; }
    public required DateTimeOffset CompletedAt { get; init; }
}

public sealed record AppointmentMissed : IntegrationEvent
{
    public required Guid AppointmentId { get; init; }
    public required Guid CaseId { get; init; }
    public required DateTimeOffset ScheduledAt { get; init; }
}
