using System.ComponentModel.DataAnnotations;

namespace PatientManagement.Application.DTOs;

public class CreatePrescriptionItemRequest
{
    [Required]
    public required string DrugName { get; set; }
    public string? Dosage { get; set; }
    public string? Frequency { get; set; }
    public string? Duration { get; set; }
    public string? Instructions { get; set; }
}

/// <summary>
/// VisitId is not a property here -- it comes from the route
/// (POST /api/visits/{visitId}/prescriptions), matching
/// StartConsultationRequest's split between route id and body.
///
/// [MinLength(1)] on Items (implementation-time decision, not called out in
/// the module's own checklist, cheap to relax): a Prescription with zero
/// medication lines isn't a meaningful printed prescription, so creation is
/// rejected with a 400 rather than allowed to produce an empty row.
/// </summary>
public class CreatePrescriptionRequest
{
    [Required]
    [MinLength(1)]
    public required List<CreatePrescriptionItemRequest> Items { get; set; }
}
