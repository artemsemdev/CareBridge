using CareBridge.Shared.Contracts.Enums;

namespace CareBridge.CareGapEngine.Services;

// Security: Threshold values are clinical safety boundaries based on medical reference standards.
// Changes to these values affect patient safety alerting — require clinical review before modification.
// PHI: This evaluator processes observation values (vital signs) but does not store or log them.
/// <summary>
/// Evaluates a single observation value against the MVP hardcoded threshold rules and returns
/// the highest-severity alert info if any threshold is exceeded, or null for normal values.
/// Weight-change detection (3 kg in 3 days) is deferred — only absolute thresholds are implemented.
/// </summary>
public static class ThresholdEvaluator
{
    public record AlertInfo(Severity Severity, string Title, string Description);

    public static AlertInfo? Evaluate(ObservationType type, decimal value)
    {
        return type switch
        {
            ObservationType.BloodPressure => EvaluateBloodPressure(value),
            ObservationType.HeartRate => EvaluateHeartRate(value),
            ObservationType.SpO2 => EvaluateSpO2(value),
            ObservationType.Glucose => EvaluateGlucose(value),
            ObservationType.Temperature => EvaluateTemperature(value),
            ObservationType.Weight => null, // Weight change rule deferred (requires history comparison)
            _ => null
        };
    }

    private static AlertInfo? EvaluateBloodPressure(decimal value)
    {
        if (value > 180)
            return new AlertInfo(Severity.Critical, "Critical BP Reading",
                $"Critical: Systolic BP {value} mmHg exceeds 180");
        if (value > 160)
            return new AlertInfo(Severity.High, "High BP Reading",
                $"High: Systolic BP {value} mmHg exceeds 160");
        if (value > 140)
            return new AlertInfo(Severity.Medium, "Elevated BP Reading",
                $"Elevated: Systolic BP {value} mmHg exceeds 140");
        return null;
    }

    private static AlertInfo? EvaluateHeartRate(decimal value)
    {
        if (value > 120 || value < 50)
            return new AlertInfo(Severity.High, "Abnormal Heart Rate",
                $"Heart rate {value} bpm outside normal range (50–120)");
        return null;
    }

    private static AlertInfo? EvaluateSpO2(decimal value)
    {
        if (value < 90)
            return new AlertInfo(Severity.Critical, "Critical SpO2 Reading",
                $"Critical: SpO2 {value}% below 90");
        if (value < 94)
            return new AlertInfo(Severity.High, "Low SpO2 Reading",
                $"Low: SpO2 {value}% below 94");
        return null;
    }

    private static AlertInfo? EvaluateGlucose(decimal value)
    {
        if (value > 300 || value < 70)
            return new AlertInfo(Severity.High, "Glucose Out of Range",
                $"Glucose {value} mg/dL outside safe range (70–300)");
        return null;
    }

    private static AlertInfo? EvaluateTemperature(decimal value)
    {
        if (value > 38.5m)
            return new AlertInfo(Severity.Medium, "Elevated Temperature",
                $"Elevated temperature: {value}°C");
        return null;
    }
}
