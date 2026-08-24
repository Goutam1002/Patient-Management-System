using System.Net;
using System.Net.Http.Json;
using PatientManagement.Application.DTOs;

namespace PatientManagement.Api.Tests;

public class AuthControllerTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Login_with_correct_credentials_succeeds_and_returns_a_session_token()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = AuthApiFactory.SeedUsername,
            Password = AuthApiFactory.SeedPassword,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<LoginResult>();
        Assert.NotNull(result);
        Assert.Equal(AuthApiFactory.SeedUsername, result!.Username);
        Assert.False(string.IsNullOrWhiteSpace(result.SessionToken));
    }

    [Fact]
    public async Task Login_with_wrong_username_is_rejected()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = "not-the-doctor",
            Password = AuthApiFactory.SeedPassword,
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_with_wrong_password_is_rejected()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = AuthApiFactory.SeedUsername,
            Password = "WrongPassword!",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_with_empty_credentials_is_rejected_as_a_bad_request()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = "",
            Password = "",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_with_missing_body_is_rejected_as_a_bad_request()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task No_registration_endpoint_exists()
    {
        // Authenticate first so the assertion isolates "route doesn't exist"
        // from "request wasn't authenticated" -- the pipeline's fallback
        // policy returns 401 for an unmatched route when unauthenticated,
        // by design (it doesn't leak route existence to anonymous callers).
        using var request = await AuthenticatedRequestAsync(HttpMethod.Post, "/api/auth/register");
        request.Content = JsonContent.Create(new LoginRequest { Username = "new-doctor", Password = "whatever" });

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task No_password_reset_endpoint_exists()
    {
        using var request = await AuthenticatedRequestAsync(HttpMethod.Post, "/api/auth/reset-password");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<HttpRequestMessage> AuthenticatedRequestAsync(HttpMethod method, string requestUri)
    {
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = AuthApiFactory.SeedUsername,
            Password = AuthApiFactory.SeedPassword,
        });
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResult>();

        var request = new HttpRequestMessage(method, requestUri);
        request.Headers.Add(PatientManagement.Api.Authentication.SessionTokenDefaults.HeaderName, login!.SessionToken);
        return request;
    }
}
