using PatientManagement.Application.DTOs;

namespace PatientManagement.Application.Services;

public interface IDoctorDetailsService
{
    /// <summary>
    /// Returns the single DoctorDetails row, or sensible defaults if it
    /// hasn't been saved yet -- does not create a row itself.
    /// </summary>
    Task<DoctorDetailsDto> GetAsync();

    /// <summary>
    /// Get-or-create singleton update: updates the one existing row, or
    /// creates it if this is the first save. Never results in more than one row.
    /// </summary>
    Task<DoctorDetailsDto> UpdateAsync(UpdateDoctorDetailsRequest request);
}
