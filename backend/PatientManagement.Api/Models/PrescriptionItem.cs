namespace PatientManagement.Api.Models;

/// <summary>
/// One medication line on a Prescription. Free text with autocomplete
/// (autocomplete is a UX assist built later, not a validation constraint) --
/// not a coded/structured drug database, so DrugName has no dictionary FK.
/// </summary>
public class PrescriptionItem
{
    public int Id { get; set; }
    public int PrescriptionId { get; set; }
    public Prescription? Prescription { get; set; }

    public required string DrugName { get; set; }
    public string? Dosage { get; set; }
    public string? Frequency { get; set; }
    public string? Duration { get; set; }
    public string? Instructions { get; set; }
}
