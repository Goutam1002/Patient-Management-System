namespace PatientManagement.Application.DTOs;

public class DoctorDetailsDto
{
    public int Id { get; set; }
    public required string ClinicName { get; set; }
    public required string DoctorName { get; set; }
    public string? Qualifications { get; set; }
    public string? RegistrationNumber { get; set; }

    // Base64-encoded image bytes -- the wire-friendly shape of DoctorDetails.Logo/Signature.
    public string? Logo { get; set; }
    public string? Signature { get; set; }
}
