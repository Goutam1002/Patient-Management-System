namespace PatientManagement.Application.DTOs;

/// <summary>
/// What the walk-in endpoint returns: proof that exactly one Appointment and
/// one linked Visit were created together. The Visit's clinical content
/// (vitals/complaints/diagnosis) is deliberately NOT echoed back here --
/// Module 5 (Consultation Workflow) owns the clinical read surface; this
/// module owns the visit's existence and its linkage to an appointment.
/// </summary>
public class WalkInVisitDto
{
    public int VisitId { get; set; }
    public int AppointmentId { get; set; }
    public int PatientId { get; set; }

    /// <summary>Sequential per patient (1, 2, 3, ...), not a global counter.</summary>
    public int VisitNumber { get; set; }
}
