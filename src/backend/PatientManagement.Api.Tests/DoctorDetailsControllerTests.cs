using System.Net;
using System.Net.Http.Json;
using PatientManagement.Api.Authentication;
using PatientManagement.Application.DTOs;

namespace PatientManagement.Api.Tests;

/// <summary>
/// Deliberately does NOT share an AuthApiFactory via IClassFixture the way
/// AuthControllerTests does -- PUT here mutates the singleton DoctorDetails
/// row, so each test gets its own fresh factory/database (xUnit creates a
/// new test-class instance per test method by default) to stay independent.
/// </summary>
public class DoctorDetailsControllerTests : IDisposable
{
    private readonly AuthApiFactory _factory = new();
    private readonly HttpClient _client;

    public DoctorDetailsControllerTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Get_before_any_save_returns_sensible_defaults()
    {
        using var request = await AuthenticatedRequestAsync(HttpMethod.Get, "/api/doctor-details");
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<DoctorDetailsDto>();
        Assert.NotNull(result);
        Assert.Equal(string.Empty, result!.ClinicName);
        Assert.Equal(string.Empty, result.DoctorName);
        Assert.Null(result.Logo);
    }

    [Fact]
    public async Task Update_persists_and_a_subsequent_get_reflects_it()
    {
        using var putRequest = await AuthenticatedRequestAsync(HttpMethod.Put, "/api/doctor-details");
        putRequest.Content = JsonContent.Create(new UpdateDoctorDetailsRequest
        {
            ClinicName = "Sunrise Clinic",
            DoctorName = "Dr. Rao",
            Qualifications = "MBBS, MD",
            RegistrationNumber = "REG-123",
        });
        var putResponse = await _client.SendAsync(putRequest);
        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);

        using var getRequest = await AuthenticatedRequestAsync(HttpMethod.Get, "/api/doctor-details");
        var getResponse = await _client.SendAsync(getRequest);
        var result = await getResponse.Content.ReadFromJsonAsync<DoctorDetailsDto>();

        Assert.NotNull(result);
        Assert.Equal("Sunrise Clinic", result!.ClinicName);
        Assert.Equal("Dr. Rao", result.DoctorName);
        Assert.Equal("MBBS, MD", result.Qualifications);
        Assert.Equal("REG-123", result.RegistrationNumber);
    }

    [Fact]
    public async Task Two_updates_never_create_a_second_row()
    {
        using var firstPut = await AuthenticatedRequestAsync(HttpMethod.Put, "/api/doctor-details");
        firstPut.Content = JsonContent.Create(new UpdateDoctorDetailsRequest { ClinicName = "First Clinic", DoctorName = "Dr. A" });
        var firstResponse = await _client.SendAsync(firstPut);
        var first = await firstResponse.Content.ReadFromJsonAsync<DoctorDetailsDto>();

        using var secondPut = await AuthenticatedRequestAsync(HttpMethod.Put, "/api/doctor-details");
        secondPut.Content = JsonContent.Create(new UpdateDoctorDetailsRequest { ClinicName = "Second Clinic", DoctorName = "Dr. B" });
        var secondResponse = await _client.SendAsync(secondPut);
        var second = await secondResponse.Content.ReadFromJsonAsync<DoctorDetailsDto>();

        // Same row updated in place, not a new one created.
        Assert.Equal(first!.Id, second!.Id);

        using var getRequest = await AuthenticatedRequestAsync(HttpMethod.Get, "/api/doctor-details");
        var getResponse = await _client.SendAsync(getRequest);
        var result = await getResponse.Content.ReadFromJsonAsync<DoctorDetailsDto>();
        Assert.Equal("Second Clinic", result!.ClinicName);
    }

    [Fact]
    public async Task Logo_and_signature_round_trip_as_bytes()
    {
        var logoBytes = new byte[] { 10, 20, 30, 40 };
        var signatureBytes = new byte[] { 99, 88, 77 };

        using var putRequest = await AuthenticatedRequestAsync(HttpMethod.Put, "/api/doctor-details");
        putRequest.Content = JsonContent.Create(new UpdateDoctorDetailsRequest
        {
            ClinicName = "Clinic",
            DoctorName = "Doctor",
            Logo = Convert.ToBase64String(logoBytes),
            Signature = Convert.ToBase64String(signatureBytes),
        });
        var putResponse = await _client.SendAsync(putRequest);
        var updated = await putResponse.Content.ReadFromJsonAsync<DoctorDetailsDto>();

        Assert.Equal(logoBytes, Convert.FromBase64String(updated!.Logo!));
        Assert.Equal(signatureBytes, Convert.FromBase64String(updated.Signature!));
    }

    [Fact]
    public async Task Omitting_logo_on_a_later_update_leaves_the_existing_logo_unchanged()
    {
        var logoBytes = Convert.ToBase64String([5, 6, 7]);
        using var firstPut = await AuthenticatedRequestAsync(HttpMethod.Put, "/api/doctor-details");
        firstPut.Content = JsonContent.Create(new UpdateDoctorDetailsRequest { ClinicName = "Clinic", DoctorName = "Doctor", Logo = logoBytes });
        await _client.SendAsync(firstPut);

        using var secondPut = await AuthenticatedRequestAsync(HttpMethod.Put, "/api/doctor-details");
        secondPut.Content = JsonContent.Create(new UpdateDoctorDetailsRequest { ClinicName = "Clinic Renamed", DoctorName = "Doctor" });
        var secondResponse = await _client.SendAsync(secondPut);
        var result = await secondResponse.Content.ReadFromJsonAsync<DoctorDetailsDto>();

        Assert.Equal("Clinic Renamed", result!.ClinicName);
        Assert.Equal(logoBytes, result.Logo);
    }

    [Fact]
    public async Task Endpoint_requires_authentication()
    {
        var response = await _client.GetAsync("/api/doctor-details");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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
        request.Headers.Add(SessionTokenDefaults.HeaderName, login!.SessionToken);
        return request;
    }
}
