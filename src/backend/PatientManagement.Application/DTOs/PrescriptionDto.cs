namespace PatientManagement.Application.DTOs;

/// <summary>
/// The full read/print shape for a Prescription -- the doctor/clinic header
/// fields are exactly what was snapshotted at creation time (never a live
/// join back to DoctorDetails), plus every line item. Logo/Signature are
/// exposed as base64 strings, mirroring DoctorDetailsDto's own convention
/// for the same byte[] columns.
/// </summary>
public class PrescriptionDto
{
    public int Id { get; set; }
    public int VisitId { get; set; }
    public DateTime CreatedAt { get; set; }
    public required string ClinicName { get; set; }
    public required string DoctorName { get; set; }
    public string? Qualifications { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? Logo { get; set; }
    public string? Signature { get; set; }
    public List<PrescriptionItemDto> Items { get; set; } = [];
}
