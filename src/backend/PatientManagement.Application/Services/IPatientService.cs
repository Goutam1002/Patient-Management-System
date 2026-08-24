using PatientManagement.Application.DTOs;

namespace PatientManagement.Application.Services;

public interface IPatientService
{
    Task<PatientDto> CreateAsync(CreatePatientRequest request);

    /// <summary>Returns null if no patient exists with the given id.</summary>
    Task<PatientDto?> GetAsync(int patientId);

    /// <summary>Returns null if no patient exists with the given id.</summary>
    Task<PatientDto?> UpdateAsync(int patientId, UpdatePatientRequest request);

    /// <summary>
    /// Case-insensitive contains-semantics search on Name and/or Phone.
    /// Neither parameter given returns no results -- a search endpoint
    /// with no terms isn't a "list all patients" endpoint.
    /// </summary>
    Task<IReadOnlyList<PatientDto>> SearchAsync(string? name, string? phone);
}
