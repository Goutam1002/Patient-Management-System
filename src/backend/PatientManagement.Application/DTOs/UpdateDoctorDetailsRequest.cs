using System.ComponentModel.DataAnnotations;

namespace PatientManagement.Application.DTOs;

public class UpdateDoctorDetailsRequest
{
    [Required]
    public required string ClinicName { get; set; }

    [Required]
    public required string DoctorName { get; set; }

    public string? Qualifications { get; set; }
    public string? RegistrationNumber { get; set; }

    // Base64-encoded image bytes. Null means "leave the existing image
    // unchanged" -- the Angular form only sends bytes when the doctor picks
    // a new file, unlike the plain text fields above which fully replace on
    // every save.
    public string? Logo { get; set; }
    public string? Signature { get; set; }
}
