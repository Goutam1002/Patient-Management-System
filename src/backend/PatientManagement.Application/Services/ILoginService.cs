using PatientManagement.Application.DTOs;

namespace PatientManagement.Application.Services;

/// <summary>
/// Single-doctor credential check. Returns null on any failure (unknown
/// username or wrong password) -- callers must not distinguish the two in
/// the response, so a wrong-password attempt can't be used to enumerate
/// valid usernames.
/// </summary>
public interface ILoginService
{
    Task<LoginResult?> LoginAsync(LoginRequest request);
}
