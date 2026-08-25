using PatientManagement.Application.DTOs;

namespace PatientManagement.Application.Services;

public interface IConsultationService
{
    /// <summary>
    /// Records vitals (+ optional complaints/diagnosis) for a scheduled
    /// appointment, creating the linked Visit and marking the appointment
    /// Completed as a side effect -- mirrors WalkInService's rule that
    /// Completed is only ever set by recording a visit, never by hand.
    /// Returns null if no appointment exists with the given id. Throws
    /// <see cref="ConsultationAlreadyStartedException"/> if that appointment
    /// already has a visit.
    /// </summary>
    Task<VisitDto?> StartConsultationAsync(int appointmentId, StartConsultationRequest request);

    /// <summary>Returns null if no visit exists with the given id.</summary>
    Task<VisitDto?> GetAsync(int visitId);

    /// <summary>
    /// Updates only Complaints/Diagnosis on an existing visit -- vitals are
    /// never touched by this path (see UpdateVisitRequest). Returns null if
    /// no visit exists with the given id.
    /// </summary>
    Task<VisitDto?> UpdateAsync(int visitId, UpdateVisitRequest request);
}
