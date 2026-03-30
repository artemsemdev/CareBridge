using CareBridge.CareGapEngine.Services;
using CareBridge.Shared.Contracts.Enums;

namespace CareBridge.CareGapEngine.UnitTests;

public class ThresholdEvaluatorTests
{
    // ── BloodPressure ────────────────────────────────────────────────────────────

    [Fact]
    public void BloodPressure_Normal_ReturnsNull()
    {
        Assert.Null(ThresholdEvaluator.Evaluate(ObservationType.BloodPressure, 120));
        Assert.Null(ThresholdEvaluator.Evaluate(ObservationType.BloodPressure, 140));
    }

    [Fact]
    public void BloodPressure_AboveElevatedThreshold_ReturnsMedium()
    {
        var result = ThresholdEvaluator.Evaluate(ObservationType.BloodPressure, 141);
        Assert.NotNull(result);
        Assert.Equal(Severity.Medium, result.Severity);
    }

    [Fact]
    public void BloodPressure_AboveHighThreshold_ReturnsHigh()
    {
        var result = ThresholdEvaluator.Evaluate(ObservationType.BloodPressure, 165);
        Assert.NotNull(result);
        Assert.Equal(Severity.High, result.Severity);
    }

    [Fact]
    public void BloodPressure_AboveCriticalThreshold_ReturnsCritical()
    {
        var result = ThresholdEvaluator.Evaluate(ObservationType.BloodPressure, 185);
        Assert.NotNull(result);
        Assert.Equal(Severity.Critical, result.Severity);
    }

    [Fact]
    public void BloodPressure_CriticalHasHigherSeverityThanHigh()
    {
        // Verifies highest-severity-wins rule: 185 should be Critical, not High or Medium
        var result = ThresholdEvaluator.Evaluate(ObservationType.BloodPressure, 185);
        Assert.Equal(Severity.Critical, result!.Severity);
    }

    // ── HeartRate ────────────────────────────────────────────────────────────────

    [Fact]
    public void HeartRate_Normal_ReturnsNull()
    {
        Assert.Null(ThresholdEvaluator.Evaluate(ObservationType.HeartRate, 70));
        Assert.Null(ThresholdEvaluator.Evaluate(ObservationType.HeartRate, 50));
        Assert.Null(ThresholdEvaluator.Evaluate(ObservationType.HeartRate, 120));
    }

    [Fact]
    public void HeartRate_TooHigh_ReturnsHigh()
    {
        var result = ThresholdEvaluator.Evaluate(ObservationType.HeartRate, 125);
        Assert.NotNull(result);
        Assert.Equal(Severity.High, result.Severity);
    }

    [Fact]
    public void HeartRate_TooLow_ReturnsHigh()
    {
        var result = ThresholdEvaluator.Evaluate(ObservationType.HeartRate, 45);
        Assert.NotNull(result);
        Assert.Equal(Severity.High, result.Severity);
    }

    // ── SpO2 ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void SpO2_Normal_ReturnsNull()
    {
        Assert.Null(ThresholdEvaluator.Evaluate(ObservationType.SpO2, 98));
        Assert.Null(ThresholdEvaluator.Evaluate(ObservationType.SpO2, 94));
    }

    [Fact]
    public void SpO2_BelowHighThreshold_ReturnsHigh()
    {
        var result = ThresholdEvaluator.Evaluate(ObservationType.SpO2, 92);
        Assert.NotNull(result);
        Assert.Equal(Severity.High, result.Severity);
    }

    [Fact]
    public void SpO2_BelowCriticalThreshold_ReturnsCritical()
    {
        var result = ThresholdEvaluator.Evaluate(ObservationType.SpO2, 88);
        Assert.NotNull(result);
        Assert.Equal(Severity.Critical, result.Severity);
    }

    // ── Glucose ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Glucose_Normal_ReturnsNull()
    {
        Assert.Null(ThresholdEvaluator.Evaluate(ObservationType.Glucose, 100));
        Assert.Null(ThresholdEvaluator.Evaluate(ObservationType.Glucose, 70));
        Assert.Null(ThresholdEvaluator.Evaluate(ObservationType.Glucose, 300));
    }

    [Fact]
    public void Glucose_TooHigh_ReturnsHigh()
    {
        var result = ThresholdEvaluator.Evaluate(ObservationType.Glucose, 310);
        Assert.NotNull(result);
        Assert.Equal(Severity.High, result.Severity);
    }

    [Fact]
    public void Glucose_TooLow_ReturnsHigh()
    {
        var result = ThresholdEvaluator.Evaluate(ObservationType.Glucose, 60);
        Assert.NotNull(result);
        Assert.Equal(Severity.High, result.Severity);
    }

    // ── Temperature ──────────────────────────────────────────────────────────────

    [Fact]
    public void Temperature_Normal_ReturnsNull()
    {
        Assert.Null(ThresholdEvaluator.Evaluate(ObservationType.Temperature, 37.0m));
        Assert.Null(ThresholdEvaluator.Evaluate(ObservationType.Temperature, 38.5m));
    }

    [Fact]
    public void Temperature_Elevated_ReturnsMedium()
    {
        var result = ThresholdEvaluator.Evaluate(ObservationType.Temperature, 38.6m);
        Assert.NotNull(result);
        Assert.Equal(Severity.Medium, result.Severity);
    }

    // ── Weight (deferred) ────────────────────────────────────────────────────────

    [Fact]
    public void Weight_AlwaysReturnsNull_ChangeRuleDeferred()
    {
        // Weight change detection (3 kg in 3 days) is deferred — absolute threshold not implemented
        Assert.Null(ThresholdEvaluator.Evaluate(ObservationType.Weight, 100));
        Assert.Null(ThresholdEvaluator.Evaluate(ObservationType.Weight, 200));
    }

    // ── Alert content ────────────────────────────────────────────────────────────

    [Fact]
    public void BloodPressure_Critical_AlertContainsValue()
    {
        var result = ThresholdEvaluator.Evaluate(ObservationType.BloodPressure, 195);
        Assert.NotNull(result);
        Assert.Contains("195", result.Description);
        Assert.False(string.IsNullOrWhiteSpace(result.Title));
    }

    [Fact]
    public void SpO2_Critical_AlertContainsValue()
    {
        var result = ThresholdEvaluator.Evaluate(ObservationType.SpO2, 85);
        Assert.NotNull(result);
        Assert.Contains("85", result.Description);
    }
}
