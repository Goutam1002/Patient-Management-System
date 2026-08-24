namespace PatientManagement.Domain.Models;

public class Patient
{
    // Sequential integer starting at 0 (IDENTITY(0,1), configured in AppDbContext) --
    // the durable identifier used for search, cross-reference, and CSV export.
    public int PatientId { get; set; }

    public required string Name { get; set; }

    // Both captured and persisted independently -- neither is derived from the
    // other. "Age / DOB" in the BRD is read as "capture whichever is known",
    // so both are nullable rather than both mandatory.
    public int? Age { get; set; }
    public DateOnly? DateOfBirth { get; set; }

    public required string Gender { get; set; }

    // Optional and non-unique -- multiple patients may legitimately share a phone.
    public string? Phone { get; set; }

    public string? Allergies { get; set; }
    public string? CurrentMedications { get; set; }
    public string? ChronicConditions { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }

    // No MedicalSurgicalHistory field -- explicit Phase 1 exclusion, see
    // implementation-brd.md. Do not add one unprompted.
}
