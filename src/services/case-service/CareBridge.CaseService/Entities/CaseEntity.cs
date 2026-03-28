using CareBridge.Shared.Contracts.Enums;

namespace CareBridge.CaseService.Entities;

public class CaseEntity
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string DiagnosisCode { get; set; } = string.Empty;
    public string DiagnosisDescription { get; set; } = string.Empty;
    public DateTimeOffset DischargeDate { get; set; }
    public CaseStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
