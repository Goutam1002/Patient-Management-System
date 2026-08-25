namespace PatientManagement.Application.Services;

/// <summary>
/// Thrown when starting a consultation would create a second Visit for an
/// appointment that already has one (an appointment produces at most one
/// visit -- see the unique index on Visit.AppointmentId). A future
/// controller maps this to an HTTP 409, not a 500 -- this is an expected,
/// named rejection, same pattern as AppointmentSlotConflictException.
/// </summary>
public class ConsultationAlreadyStartedException(int appointmentId)
    : Exception($"Appointment {appointmentId} already has a visit recorded.")
{
    public int AppointmentId { get; } = appointmentId;
}
