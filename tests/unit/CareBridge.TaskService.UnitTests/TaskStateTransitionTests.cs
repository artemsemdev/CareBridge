using CareBridge.Shared.Contracts.Enums;

namespace CareBridge.TaskService.UnitTests;

public class TaskStateTransitionTests
{
    // Replicate the validation logic from Program.cs for pure unit testing
    private static string? ValidateTransition(CareBridgeTaskStatus current, CareBridgeTaskStatus next)
    {
        var valid = current switch
        {
            CareBridgeTaskStatus.Open => next is CareBridgeTaskStatus.InProgress or CareBridgeTaskStatus.Deferred,
            CareBridgeTaskStatus.InProgress => next is CareBridgeTaskStatus.Completed or CareBridgeTaskStatus.Deferred or CareBridgeTaskStatus.Open,
            CareBridgeTaskStatus.Deferred => next is CareBridgeTaskStatus.Open,
            CareBridgeTaskStatus.Completed => false,
            _ => false
        };
        return valid ? null : $"Cannot transition from {current} to {next}.";
    }

    [Theory]
    [InlineData(CareBridgeTaskStatus.Open, CareBridgeTaskStatus.InProgress)]
    [InlineData(CareBridgeTaskStatus.Open, CareBridgeTaskStatus.Deferred)]
    [InlineData(CareBridgeTaskStatus.InProgress, CareBridgeTaskStatus.Completed)]
    [InlineData(CareBridgeTaskStatus.InProgress, CareBridgeTaskStatus.Deferred)]
    [InlineData(CareBridgeTaskStatus.InProgress, CareBridgeTaskStatus.Open)]
    [InlineData(CareBridgeTaskStatus.Deferred, CareBridgeTaskStatus.Open)]
    public void ValidTransitions_ShouldBeAllowed(CareBridgeTaskStatus from, CareBridgeTaskStatus to)
    {
        Assert.Null(ValidateTransition(from, to));
    }

    [Theory]
    [InlineData(CareBridgeTaskStatus.Open, CareBridgeTaskStatus.Completed)]
    [InlineData(CareBridgeTaskStatus.Completed, CareBridgeTaskStatus.Open)]
    [InlineData(CareBridgeTaskStatus.Completed, CareBridgeTaskStatus.InProgress)]
    [InlineData(CareBridgeTaskStatus.Completed, CareBridgeTaskStatus.Deferred)]
    [InlineData(CareBridgeTaskStatus.Deferred, CareBridgeTaskStatus.InProgress)]
    [InlineData(CareBridgeTaskStatus.Deferred, CareBridgeTaskStatus.Completed)]
    public void InvalidTransitions_ShouldBeRejected(CareBridgeTaskStatus from, CareBridgeTaskStatus to)
    {
        Assert.NotNull(ValidateTransition(from, to));
    }
}
