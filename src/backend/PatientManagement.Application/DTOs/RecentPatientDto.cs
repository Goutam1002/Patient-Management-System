namespace PatientManagement.Application.DTOs;

/// <summary>
/// A patient row in the "recent patients" list -- Module 8's own read shape.
/// LastVisitDate is null for a patient with no visits yet; such patients are
/// ranked last (see IRecentPatientsService.GetRecentAsync), not excluded.
/// </summary>
public class RecentPatientDto
{
    public int PatientId { get; set; }
    public required string Name { get; set; }
    public string? Phone { get; set; }
    public DateTime? LastVisitDate { get; set; }
}
