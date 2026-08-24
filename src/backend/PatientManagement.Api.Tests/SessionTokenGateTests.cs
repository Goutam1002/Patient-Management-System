using System.Net;
using System.Net.Http.Json;
using PatientManagement.Api.Authentication;
using PatientManagement.Application.DTOs;

namespace PatientManagement.Api.Tests;

/// <summary>
/// Proves the pipeline actually enforces the fallback RequireAuthenticatedUser()
/// policy from Program.cs, against TestOnlyProtectedController (see its own
/// doc comment for why this test-only controller exists).
/// </summary>
public class SessionTokenGateTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Request_without_a_session_token_is_rejected()
    {
        var response = await _client.GetAsync("/api/test/protected");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Request_with_an_invalid_session_token_is_rejected()
    {
        _client.DefaultRequestHeaders.Add(SessionTokenDefaults.HeaderName, "not-a-real-token");

        var response = await _client.GetAsync("/api/test/protected");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Request_with_the_token_issued_at_login_is_accepted()
    {
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = AuthApiFactory.SeedUsername,
            Password = AuthApiFactory.SeedPassword,
        });
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResult>();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/test/protected");
        request.Headers.Add(SessionTokenDefaults.HeaderName, login!.SessionToken);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var whoAmI = await response.Content.ReadAsStringAsync();
        Assert.Contains(AuthApiFactory.SeedUsername, whoAmI);
    }

    [Fact]
    public async Task Login_itself_requires_no_session_token()
    {
        // No X-Session-Token header attached -- proves [AllowAnonymous] on
        // AuthController.Login overrides the fallback policy correctly.
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = AuthApiFactory.SeedUsername,
            Password = AuthApiFactory.SeedPassword,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
