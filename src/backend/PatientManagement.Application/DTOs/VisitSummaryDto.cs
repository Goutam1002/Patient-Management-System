namespace PatientManagement.Application.DTOs;

/// <summary>
/// The per-visit history list row for a patient -- Module 7's own read shape,
/// distinct from Module 5's VisitDto (which is the create/edit surface).
/// VisitDate is computed from Appointment.ScheduledTime (Visit has no own
/// date column) -- see Modules/07-patient-history.md's Business Rules
/// section; Modules/09-data-export.md resolves the same gap the same way.
/// </summary>
public class VisitSummaryDto
{
    public int Id { get; set; }
    public int PatientId { get; set; }

    /// <summary>Sequential per patient (1, 2, 3, ...), not a global counter.</summary>
    public int VisitNumber { get; set; }

    public DateTime VisitDate { get; set; }
    public string? Diagnosis { get; set; }
}
