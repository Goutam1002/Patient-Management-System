using System.ComponentModel.DataAnnotations;
using PatientManagement.Domain.Models;

namespace PatientManagement.Application.DTOs;

public class UpdateAppointmentStatusRequest
{
    /// <summary>
    /// Nullable + [Required] so an omitted status is a 400 rather than
    /// binding to the enum's default member (Scheduled).
    /// </summary>
    [Required]
    public AppointmentStatus? Status { get; set; }
}
