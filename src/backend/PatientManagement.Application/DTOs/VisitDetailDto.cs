namespace PatientManagement.Application.DTOs;

/// <summary>
/// The full per-visit history detail -- vitals, complaints, diagnosis, and
/// every prescription recorded at that visit, plus the computed VisitDate
/// (see VisitSummaryDto). This is the richer shape GET /api/visits/{id}
/// returns (Module 7 extends Module 5's existing endpoint rather than
/// duplicating the route -- see VisitsController).
/// </summary>
public class VisitDetailDto
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public int AppointmentId { get; set; }

    /// <summary>Sequential per patient (1, 2, 3, ...), not a global counter.</summary>
    public int VisitNumber { get; set; }

    public DateTime VisitDate { get; set; }

    public decimal Temperature { get; set; }
    public short BpSystolic { get; set; }
    public short BpDiastolic { get; set; }
    public int Pulse { get; set; }
    public decimal Weight { get; set; }

    public string? Complaints { get; set; }
    public string? Diagnosis { get; set; }

    public List<PrescriptionDto> Prescriptions { get; set; } = [];
}
