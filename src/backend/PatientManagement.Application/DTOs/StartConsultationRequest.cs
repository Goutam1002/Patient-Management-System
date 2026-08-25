using System.ComponentModel.DataAnnotations;

namespace PatientManagement.Application.DTOs;

/// <summary>
/// Vitals (+ optional complaints/diagnosis) for starting a consultation from
/// a scheduled appointment. Every vital is deliberately nullable + [Required]
/// -- the same technique CreateAppointmentRequest.DurationMinutes uses --
/// rather than plain non-nullable value types the way WalkInVisitRequest
/// does. WalkInVisitRequest's non-nullable primitives let an omitted field
/// silently bind to 0 (a "valid" vital), which would defeat the module's own
/// required test ("visit cannot be saved with missing temperature/BP/pulse
/// -- server-side"). Nullable + [Required] makes ASP.NET Core's automatic
/// model validation reject a missing vital with a 400 before the request
/// even reaches ConsultationService, so mandatory-at-entry is enforced by
/// the API layer, not just by the non-nullable Visit column underneath it.
/// </summary>
public class StartConsultationRequest
{
    [Required]
    public decimal? Temperature { get; set; }

    [Required]
    public short? BpSystolic { get; set; }

    [Required]
    public short? BpDiastolic { get; set; }

    [Required]
    public int? Pulse { get; set; }

    [Required]
    public decimal? Weight { get; set; }

    public string? Complaints { get; set; }
    public string? Diagnosis { get; set; }
}
