using CareBridge.Shared.Contracts.Enums;

namespace CareBridge.CaseService.Entities;

// PHI: This entity contains Protected Health Information (patient name, diagnosis).
// All access to this entity must be through authorized API endpoints.
// Retention: Cases use soft-delete (Status=Closed). No hard-delete operation exists.
// Closed cases remain queryable for audit trail integrity per HIPAA §164.530(j).
public class CaseEntity
{
    public Guid Id { get; set; }
    public string PatientId { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public string DiagnosisCode { get; set; } = string.Empty;
    public string DiagnosisDescription { get; set; } = string.Empty;
    public DateTimeOffset DischargeDate { get; set; }
    public CaseStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
