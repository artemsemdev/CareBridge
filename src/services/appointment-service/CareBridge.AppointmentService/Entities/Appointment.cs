using CareBridge.Shared.Contracts.Enums;

namespace CareBridge.AppointmentService.Entities;

// Retention: Appointments are not hard-deleted. Terminal-status appointments (Completed, Canceled, NoShow)
// remain queryable for audit trail integrity per HIPAA §164.530(j).
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
