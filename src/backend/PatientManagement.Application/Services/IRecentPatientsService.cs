using PatientManagement.Application.DTOs;

namespace PatientManagement.Application.Services;

public interface IRecentPatientsService
{
    /// <summary>
    /// The <paramref name="count"/> most recently visited patients, ordered by
    /// most-recent visit date descending -- not registration date, per
    /// implementation-brd.md's fixed Search spec. Patients with no visit yet
    /// have no visit date to sort by and are placed last (implementation-time
    /// choice, documented in docs/implementation-progress.md Step 16), so
    /// they only appear once every patient with at least one visit is listed.
    /// </summary>
    Task<IReadOnlyList<RecentPatientDto>> GetRecentAsync(int count);
}
