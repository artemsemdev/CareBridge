using CareBridge.Shared.Contracts.Enums;

namespace CareBridge.AppointmentService.UnitTests;

public class AppointmentStateTransitionTests
{
    private static string? ValidateTransition(AppointmentStatus current, AppointmentStatus next)
    {
        var valid = current switch
        {
            AppointmentStatus.Proposed => next is AppointmentStatus.Booked or AppointmentStatus.Canceled,
            AppointmentStatus.Booked => next is AppointmentStatus.Completed or AppointmentStatus.Canceled or AppointmentStatus.NoShow,
            _ => false
        };
        return valid ? null : $"Cannot transition from {current} to {next}.";
    }

    [Theory]
    [InlineData(AppointmentStatus.Proposed, AppointmentStatus.Booked)]
    [InlineData(AppointmentStatus.Proposed, AppointmentStatus.Canceled)]
    [InlineData(AppointmentStatus.Booked, AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.Booked, AppointmentStatus.Canceled)]
    [InlineData(AppointmentStatus.Booked, AppointmentStatus.NoShow)]
    public void ValidTransitions_ShouldBeAllowed(AppointmentStatus from, AppointmentStatus to)
    {
        Assert.Null(ValidateTransition(from, to));
    }

    [Theory]
    [InlineData(AppointmentStatus.Proposed, AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.Proposed, AppointmentStatus.NoShow)]
    [InlineData(AppointmentStatus.Completed, AppointmentStatus.Booked)]
    [InlineData(AppointmentStatus.Completed, AppointmentStatus.Canceled)]
    [InlineData(AppointmentStatus.Canceled, AppointmentStatus.Booked)]
    [InlineData(AppointmentStatus.NoShow, AppointmentStatus.Completed)]
    public void InvalidTransitions_ShouldBeRejected(AppointmentStatus from, AppointmentStatus to)
    {
        Assert.NotNull(ValidateTransition(from, to));
    }
}
