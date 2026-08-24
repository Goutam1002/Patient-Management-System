namespace PatientManagement.Domain.Models;

/// <summary>
/// Immutable once created -- no update endpoint will ever target an existing
/// Prescription's fields or line items (enforced at the controller layer,
/// not here). A correction creates a new Prescription row instead.
/// </summary>
public class Prescription
{
    public int Id { get; set; }
    public int VisitId { get; set; }
    public Visit? Visit { get; set; }
    public DateTime CreatedAt { get; set; }

    // Snapshotted from DoctorDetails at creation time -- never a live join,
    // so a later edit to DoctorDetails doesn't retroactively change a
    // historical prescription's printed header/footer.
    public required string ClinicName { get; set; }
    public required string DoctorName { get; set; }
    public string? Qualifications { get; set; }
    public string? RegistrationNumber { get; set; }
    public byte[]? Logo { get; set; }
    public byte[]? Signature { get; set; }

    public ICollection<PrescriptionItem> Items { get; set; } = new List<PrescriptionItem>();

    /// <summary>
    /// The only sanctioned way to build a Prescription -- copies today's
    /// DoctorDetails values onto the new row rather than a live FK.
    /// </summary>
    public static Prescription CreateFromDoctorDetails(int visitId, DoctorDetails source, DateTime createdAt)
    {
        return new Prescription
        {
            VisitId = visitId,
            CreatedAt = createdAt,
            ClinicName = source.ClinicName,
            DoctorName = source.DoctorName,
            Qualifications = source.Qualifications,
            RegistrationNumber = source.RegistrationNumber,
            Logo = source.Logo,
            Signature = source.Signature,
        };
    }
}
