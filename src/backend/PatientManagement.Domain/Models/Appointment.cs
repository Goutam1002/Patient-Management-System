namespace PatientManagement.Domain.Models;

public class Appointment
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public Patient? Patient { get; set; }

    public DateTime ScheduledTime { get; set; }

    // Doctor-entered per appointment -- never a fixed system default.
    public int DurationMinutes { get; set; }

    public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;
}
