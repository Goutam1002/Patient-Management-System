namespace PatientManagement.Application.Services;

/// <summary>
/// In-memory session-token bookkeeping for the single-doctor app. Deliberately
/// not persisted -- a Kestrel restart invalidating any open session is
/// acceptable for a local desktop app the doctor restarts rarely, and it
/// avoids adding a session table/cookie-auth machinery for one account.
/// </summary>
public interface ISessionTokenStore
{
    string IssueToken(string username);

    bool TryGetUsername(string token, out string username);
}
