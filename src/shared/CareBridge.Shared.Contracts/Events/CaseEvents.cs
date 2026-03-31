using CareBridge.Shared.Contracts.Enums;

namespace CareBridge.Shared.Contracts.Events;

// PHI: CaseCreated carries patient demographics (PatientId, PatientName, DiagnosisCode).
// HIPAA Minimum Necessary: All fields are required for care plan activation and audit trail.
// Services consuming this event should not persist patient name unless needed for their function.
public sealed record CaseCreated : IntegrationEvent
{
    public required Guid CaseId { get; init; }
    public required string PatientId { get; init; }
    public required string PatientName { get; init; }
    public required string DiagnosisCode { get; init; }
    public required string DiagnosisDescription { get; init; }
    public required DateTimeOffset DischargeDate { get; init; }
    public required CaseStatus Status { get; init; }
}

public sealed record CaseUpdated : IntegrationEvent
{
    public required Guid CaseId { get; init; }
    public required CaseStatus Status { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
}
