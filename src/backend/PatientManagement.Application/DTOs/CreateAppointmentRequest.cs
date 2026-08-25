using System.ComponentModel.DataAnnotations;

namespace PatientManagement.Application.DTOs;

public class CreateAppointmentRequest
{
    public int PatientId { get; set; }

    [Required]
    public DateTime? ScheduledTime { get; set; }

    /// <summary>
    /// Deliberately nullable + [Required]: the doctor enters a duration per
    /// appointment, so an omitted value must be rejected as a bad request
    /// rather than quietly defaulting to 0 (or any other fixed number).
    /// A non-nullable int here would bind an omitted field to 0 silently,
    /// which is exactly the hardcoded-default behaviour the spec forbids.
    /// </summary>
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "DurationMinutes must be a positive number of minutes entered by the doctor.")]
    public int? DurationMinutes { get; set; }
}
