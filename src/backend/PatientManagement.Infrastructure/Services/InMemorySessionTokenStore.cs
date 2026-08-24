using System.Collections.Concurrent;
using System.Security.Cryptography;
using PatientManagement.Application.Services;

namespace PatientManagement.Infrastructure.Services;

/// <summary>
/// Registered as a singleton -- the token map must survive across requests
/// for the life of the process, unlike the scoped, per-request AppDbContext.
/// </summary>
public class InMemorySessionTokenStore : ISessionTokenStore
{
    private readonly ConcurrentDictionary<string, string> _tokensToUsernames = new();

    public string IssueToken(string username)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        _tokensToUsernames[token] = username;
        return token;
    }

    public bool TryGetUsername(string token, out string username)
    {
        return _tokensToUsernames.TryGetValue(token, out username!);
    }
}
