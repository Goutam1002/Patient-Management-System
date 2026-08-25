namespace PatientManagement.Application.DTOs;

/// <summary>
/// The full clinical read shape for a Visit -- returned by
/// POST start-consultation, GET /api/visits/{id}, and PUT /api/visits/{id}.
/// Unlike WalkInVisitDto (Module 4, which deliberately omits clinical
/// content since it only proves the Appointment+Visit pair was created),
/// this DTO is Module 5's clinical read surface and includes every vital
/// plus complaints/diagnosis.
/// </summary>
public class VisitDto
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public int AppointmentId { get; set; }

    /// <summary>Sequential per patient (1, 2, 3, ...), not a global counter.</summary>
    public int VisitNumber { get; set; }

    public decimal Temperature { get; set; }
    public short BpSystolic { get; set; }
    public short BpDiastolic { get; set; }
    public int Pulse { get; set; }
    public decimal Weight { get; set; }

    public string? Complaints { get; set; }
    public string? Diagnosis { get; set; }
}
