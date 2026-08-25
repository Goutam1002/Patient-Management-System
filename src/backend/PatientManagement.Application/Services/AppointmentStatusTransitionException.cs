using PatientManagement.Domain.Models;

namespace PatientManagement.Application.Services;

/// <summary>
/// Thrown when a status transition is requested that this application does
/// not allow to be set by hand. A controller maps this to HTTP 400 -- it is
/// an expected, named rejection of user input, not an unhandled error.
/// </summary>
public class AppointmentStatusTransitionException(AppointmentStatus requestedStatus, string reason)
    : Exception(reason)
{
    public AppointmentStatus RequestedStatus { get; } = requestedStatus;
}
