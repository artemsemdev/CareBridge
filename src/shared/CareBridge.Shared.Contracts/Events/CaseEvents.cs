using CareBridge.Shared.Contracts.Enums;

namespace CareBridge.Shared.Contracts.Events;

public sealed record CaseCreated : IntegrationEvent
{
    public required Guid CaseId { get; init; }
    public required Guid PatientId { get; init; }
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
