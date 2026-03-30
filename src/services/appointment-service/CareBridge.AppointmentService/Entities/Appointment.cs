using CareBridge.Shared.Contracts.Enums;

namespace CareBridge.AppointmentService.Entities;

public class Appointment
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public AppointmentType Type { get; set; }
    public DateTimeOffset ScheduledAt { get; set; }
    public AppointmentStatus Status { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
