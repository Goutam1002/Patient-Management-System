using PatientManagement.Application.DTOs;
using PatientManagement.Domain.Models;

namespace PatientManagement.Application.Services;

public interface IAppointmentService
{
    /// <summary>
    /// Schedules an appointment in advance (Status = Scheduled). Returns null
    /// if no patient exists with the requested id. Throws
    /// <see cref="AppointmentSlotConflictException"/> if another appointment
    /// (scheduled or walk-in) already occupies that date/time -- double
    /// booking is rejected outright, not flagged.
    /// </summary>
    Task<AppointmentDto?> CreateAsync(CreateAppointmentRequest request);

    /// <summary>
    /// Every appointment falling on the given date, ordered by time. Scheduled
    /// and walk-in entries come back in one merged list because both are plain
    /// Appointment rows -- there is no second source to join in.
    /// </summary>
    Task<IReadOnlyList<AppointmentDto>> GetDailyAsync(DateOnly date);

    /// <summary>
    /// Moves an appointment to a new status. Returns null if no appointment
    /// exists with the given id. Throws
    /// <see cref="AppointmentStatusTransitionException"/> for a transition the
    /// application does not allow to be performed by hand -- see
    /// <see cref="AppointmentStatus.Completed"/>, which is only ever set as a
    /// side effect of creating a visit.
    /// </summary>
    Task<AppointmentDto?> UpdateStatusAsync(int appointmentId, AppointmentStatus status);
}
