namespace PatientManagement.Api.Services;

/// <summary>
/// Thrown when an appointment (scheduled or walk-in) would occupy a
/// date/time slot another appointment already holds. A future controller
/// maps this to an HTTP 409, not a 500 -- this is an expected, named
/// rejection, not an unhandled error.
/// </summary>
public class AppointmentSlotConflictException(DateTime scheduledTime)
    : Exception($"An appointment already exists for {scheduledTime:O}.")
{
    public DateTime ScheduledTime { get; } = scheduledTime;
}
