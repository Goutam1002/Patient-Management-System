namespace PatientManagement.Api.Models;

/// <summary>
/// Single-row clinic/doctor profile -- the source for the header/footer of
/// printed prescriptions. Snapshotted onto each Prescription at creation time
/// (see implementation-brd.md), never joined live.
/// </summary>
public class DoctorDetails
{
    public int Id { get; set; }
    public required string ClinicName { get; set; }
    public required string DoctorName { get; set; }
    public string? Qualifications { get; set; }
    public string? RegistrationNumber { get; set; }

    // Stored directly as image bytes -- this is a local-only, single-machine
    // app with no external file/blob storage, so a byte[] column is the
    // simplest correct choice rather than introducing a file-storage layer.
    public byte[]? Logo { get; set; }
    public byte[]? Signature { get; set; }
}
