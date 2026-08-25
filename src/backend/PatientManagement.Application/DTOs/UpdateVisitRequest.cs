namespace PatientManagement.Application.DTOs;

/// <summary>
/// Post-creation edit surface for a Visit -- complaints/diagnosis only.
///
/// Edit boundary (see docs/implementation-progress.md Step 13 for the full
/// reasoning): vitals are mandatory-AT-ENTRY, non-nullable, and represent
/// the physical exam findings recorded in the moment -- editing them
/// retroactively would misrepresent what was actually measured during that
/// visit. Complaints/Diagnosis are free-text clinical notes the doctor may
/// reasonably want to correct or expand afterwards (e.g. after reviewing a
/// lab result), and implementation-brd.md never declares Visit immutable
/// the way it declares Prescription immutable -- only that visits are never
/// deleted. This DTO has no property for any vital at all, so a client
/// cannot even attempt to send one through this endpoint -- the same
/// "no update endpoint accepts it" technique already used to enforce
/// Prescription line-item immutability.
/// </summary>
public record UpdateVisitRequest(string? Complaints, string? Diagnosis);
