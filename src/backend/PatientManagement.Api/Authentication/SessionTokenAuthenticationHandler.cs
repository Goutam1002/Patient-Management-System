using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PatientManagement.Application.Services;

namespace PatientManagement.Api.Authentication;

/// <summary>
/// Validates the X-Session-Token header issued by AuthController's login
/// endpoint against ISessionTokenStore. Every controller is protected by
/// this scheme by default (see Program.cs's fallback policy) -- endpoints
/// that must stay open (login) opt out with [AllowAnonymous].
/// </summary>
public class SessionTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ISessionTokenStore sessionTokenStore)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(SessionTokenDefaults.HeaderName, out var tokenValues))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var token = tokenValues.ToString();
        if (string.IsNullOrEmpty(token) || !sessionTokenStore.TryGetUsername(token, out var username))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid or expired session token."));
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, username)],
            SessionTokenDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SessionTokenDefaults.AuthenticationScheme);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
