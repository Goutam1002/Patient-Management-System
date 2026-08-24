using PatientManagement.Application.DTOs;
using PatientManagement.Domain.Models;

namespace PatientManagement.Application.Services;

public interface IWalkInService
{
    /// <summary>
    /// Creates a same-moment Appointment (Status = Completed) and its linked
    /// Visit in a single transaction, so Visit.AppointmentId stays populated
    /// for walk-ins exactly as it does for scheduled visits. Throws
    /// AppointmentSlotConflictException if another appointment already
    /// occupies the same instant.
    /// </summary>
    Task<Visit> CreateWalkInVisitAsync(WalkInVisitRequest request);
}
