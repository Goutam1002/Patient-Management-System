using PatientManagement.Application.DTOs;

namespace PatientManagement.Application.Services;

public interface IPatientHistoryService
{
    /// <summary>
    /// Lists a patient's visits, newest-first by VisitDate. <paramref name="from"/>
    /// and <paramref name="to"/> are an optional inclusive date range filter
    /// (each bound independent of the other). Returns an empty list for an
    /// unknown patient id rather than null -- there is nothing to distinguish
    /// "unknown patient" from "known patient, no visits yet" at this layer,
    /// and neither is an error.
    /// </summary>
    Task<IReadOnlyList<VisitSummaryDto>> GetVisitsAsync(int patientId, DateOnly? from, DateOnly? to);

    /// <summary>
    /// The full clinical detail for one visit -- vitals, complaints,
    /// diagnosis, and every prescription recorded at that visit. Returns
    /// null if no visit exists with the given id.
    /// </summary>
    Task<VisitDetailDto?> GetVisitDetailAsync(int visitId);
}
