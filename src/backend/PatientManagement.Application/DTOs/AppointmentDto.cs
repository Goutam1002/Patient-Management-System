using PatientManagement.Domain.Models;

namespace PatientManagement.Application.DTOs;

/// <summary>
/// One entry in the daily schedule. Scheduled and walk-in appointments are
/// the same kind of row -- there is no walk-in flag on the entity, so the
/// daily list is naturally a single merged, time-ordered list rather than
/// two disconnected views. <see cref="VisitId"/> tells the UI whether the
/// appointment has already produced a visit.
/// </summary>
public class AppointmentDto
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public required string PatientName { get; set; }
    public DateTime ScheduledTime { get; set; }

    /// <summary>Doctor-entered per appointment -- never a system default.</summary>
    public int DurationMinutes { get; set; }

    public AppointmentStatus Status { get; set; }

    /// <summary>Null until a visit exists for this appointment.</summary>
    public int? VisitId { get; set; }
}
