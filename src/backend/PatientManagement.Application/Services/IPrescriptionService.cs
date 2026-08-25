using PatientManagement.Application.DTOs;

namespace PatientManagement.Application.Services;

public interface IPrescriptionService
{
    /// <summary>
    /// Creates a new Prescription (via Prescription.CreateFromDoctorDetails --
    /// the only sanctioned construction path) with the given line items,
    /// attached to the given visit. Returns null if no visit exists with the
    /// given id. There is deliberately no update method on this interface --
    /// a printed prescription is immutable; a correction always creates a
    /// new Prescription row via this same method, never a mutation of an
    /// existing one.
    /// </summary>
    Task<PrescriptionDto?> CreatePrescriptionAsync(int visitId, CreatePrescriptionRequest request);

    /// <summary>Returns null if no prescription exists with the given id.</summary>
    Task<PrescriptionDto?> GetAsync(int prescriptionId);
}
