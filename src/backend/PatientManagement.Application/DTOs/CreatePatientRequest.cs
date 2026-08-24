using System.ComponentModel.DataAnnotations;

namespace PatientManagement.Application.DTOs;

public class CreatePatientRequest
{
    [Required]
    public required string Name { get; set; }

    public int? Age { get; set; }
    public DateOnly? DateOfBirth { get; set; }

    [Required]
    public required string Gender { get; set; }

    public string? Phone { get; set; }
    public string? Allergies { get; set; }
    public string? CurrentMedications { get; set; }
    public string? ChronicConditions { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
}
