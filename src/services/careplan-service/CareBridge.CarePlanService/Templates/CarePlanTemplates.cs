namespace CareBridge.CarePlanService.Templates;

public record MilestoneDefinition(string Name, string Description, int DueWithinHours);

public record CarePlanTemplate(string Name, IReadOnlyList<MilestoneDefinition> MilestoneDefinitions);

public static class CarePlanTemplates
{
    public static readonly CarePlanTemplate GeneralPostDischarge = new(
        "General Post-Discharge",
        [
            new("Initial Outreach",         "Contact patient within 48 hours of discharge",            48),
            new("First Observation",        "Receive first vital sign reading from patient",           72),
            new("Follow-Up Appointment",    "Schedule and confirm follow-up appointment",              168),
            new("Care Plan Review",         "Mid-point review of care plan progress",                  336),
            new("30-Day Completion",        "Complete post-discharge monitoring period",               720)
        ]);

    public static CarePlanTemplate Resolve(string? diagnosisCode) => GeneralPostDischarge;
}
