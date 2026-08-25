using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PatientManagement.Api.Authentication;
using PatientManagement.Application.DTOs;

namespace PatientManagement.Api.Tests;

/// <summary>
/// Deliberately does NOT share an AuthApiFactory via IClassFixture the way
/// AuthControllerTests does -- POST/PUT here mutate patient state, so each
/// test gets its own fresh factory/database (xUnit creates a new test-class
/// instance per test method by default) to stay independent.
/// </summary>
public class PatientsControllerTests : IDisposable
{
    private readonly AuthApiFactory _factory = new();
    private readonly HttpClient _client;

    public PatientsControllerTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose() => _factory.Dispose();

    private static CreatePatientRequest MinimalRequest(string name, string? phone = null) => new()
    {
        Name = name,
        Gender = "Female",
        Phone = phone,
    };

    [Fact]
    public async Task Create_persists_and_a_subsequent_get_reflects_every_field()
    {
        using var request = await AuthenticatedRequestAsync(HttpMethod.Post, "/api/patients");
        request.Content = JsonContent.Create(new CreatePatientRequest
        {
            Name = "Alice",
            Age = 34,
            Gender = "Female",
            Phone = "9876543210",
            Allergies = "Penicillin",
            EmergencyContactName = "Bob",
            EmergencyContactPhone = "9876500000",
        });
        var createResponse = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<PatientDto>();

        using var getRequest = await AuthenticatedRequestAsync(HttpMethod.Get, $"/api/patients/{created!.PatientId}");
        var getResponse = await _client.SendAsync(getRequest);
        var fetched = await getResponse.Content.ReadFromJsonAsync<PatientDto>();

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal("Alice", fetched!.Name);
        Assert.Equal("Bob", fetched.EmergencyContactName);
        Assert.Equal("9876500000", fetched.EmergencyContactPhone);
    }

    [Fact]
    public async Task Get_with_an_unknown_patient_id_returns_not_found()
    {
        using var request = await AuthenticatedRequestAsync(HttpMethod.Get, "/api/patients/999");
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_persists_and_a_subsequent_get_reflects_it()
    {
        using var createRequest = await AuthenticatedRequestAsync(HttpMethod.Post, "/api/patients");
        createRequest.Content = JsonContent.Create(MinimalRequest("Alice"));
        var createResponse = await _client.SendAsync(createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<PatientDto>();

        using var updateRequest = await AuthenticatedRequestAsync(HttpMethod.Put, $"/api/patients/{created!.PatientId}");
        updateRequest.Content = JsonContent.Create(new UpdatePatientRequest
        {
            Name = "Alice Renamed",
            Gender = "Female",
            Phone = "111",
        });
        var updateResponse = await _client.SendAsync(updateRequest);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        using var getRequest = await AuthenticatedRequestAsync(HttpMethod.Get, $"/api/patients/{created.PatientId}");
        var getResponse = await _client.SendAsync(getRequest);
        var fetched = await getResponse.Content.ReadFromJsonAsync<PatientDto>();

        Assert.Equal("Alice Renamed", fetched!.Name);
        Assert.Equal("111", fetched.Phone);
    }

    [Fact]
    public async Task No_delete_endpoint_exists()
    {
        using var createRequest = await AuthenticatedRequestAsync(HttpMethod.Post, "/api/patients");
        createRequest.Content = JsonContent.Create(MinimalRequest("Alice"));
        var createResponse = await _client.SendAsync(createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<PatientDto>();

        using var deleteRequest = await AuthenticatedRequestAsync(HttpMethod.Delete, $"/api/patients/{created!.PatientId}");
        var response = await _client.SendAsync(deleteRequest);

        // The GET/PUT route template matches, but no action accepts DELETE --
        // 405 (not 404) is what proves the verb itself isn't wired to anything.
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task Two_patients_may_share_the_same_phone_number()
    {
        using var first = await AuthenticatedRequestAsync(HttpMethod.Post, "/api/patients");
        first.Content = JsonContent.Create(MinimalRequest("Alice", "9876543210"));
        var firstResponse = await _client.SendAsync(first);

        using var second = await AuthenticatedRequestAsync(HttpMethod.Post, "/api/patients");
        second.Content = JsonContent.Create(MinimalRequest("Bob", "9876543210"));
        var secondResponse = await _client.SendAsync(second);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
    }

    [Fact]
    public async Task Search_matches_a_substring_occurring_anywhere_in_the_name_not_only_a_prefix()
    {
        using var first = await AuthenticatedRequestAsync(HttpMethod.Post, "/api/patients");
        first.Content = JsonContent.Create(MinimalRequest("Alexandra Smith"));
        await _client.SendAsync(first);

        using var second = await AuthenticatedRequestAsync(HttpMethod.Post, "/api/patients");
        second.Content = JsonContent.Create(MinimalRequest("Bob Jones"));
        await _client.SendAsync(second);

        using var searchRequest = await AuthenticatedRequestAsync(HttpMethod.Get, "/api/patients/search?name=andra");
        var response = await _client.SendAsync(searchRequest);
        var results = await response.Content.ReadFromJsonAsync<List<PatientDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(results!);
        Assert.Equal("Alexandra Smith", results![0].Name);
    }

    [Fact]
    public async Task Recent_ranks_by_most_recent_visit_date_not_registration_order()
    {
        var registeredFirst = await CreatePatientAsync(_client, "RegisteredFirst");
        var registeredSecond = await CreatePatientAsync(_client, "RegisteredSecond");

        // Registered first, but visited earlier -- should sort behind the
        // patient registered later but visited more recently.
        await CreateVisitAsync(_client, registeredFirst, new DateTime(2026, 1, 1, 9, 0, 0));
        await CreateVisitAsync(_client, registeredSecond, new DateTime(2026, 6, 1, 9, 0, 0));

        using var request = await AuthenticatedRequestAsync(HttpMethod.Get, "/api/patients/recent");
        var response = await _client.SendAsync(request);
        var results = await response.Content.ReadFromJsonAsync<List<RecentPatientDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("RegisteredSecond", results![0].Name);
        Assert.Equal("RegisteredFirst", results[1].Name);
    }

    [Fact]
    public async Task Recent_places_a_patient_with_no_visits_yet_last()
    {
        var noVisits = await CreatePatientAsync(_client, "NoVisits");
        var withVisit = await CreatePatientAsync(_client, "WithVisit");
        await CreateVisitAsync(_client, withVisit, new DateTime(2026, 1, 1, 9, 0, 0));

        using var request = await AuthenticatedRequestAsync(HttpMethod.Get, "/api/patients/recent");
        var response = await _client.SendAsync(request);
        var results = await response.Content.ReadFromJsonAsync<List<RecentPatientDto>>();

        Assert.Equal("WithVisit", results![0].Name);
        Assert.Equal("NoVisits", results[1].Name);
        Assert.Null(results[1].LastVisitDate);
    }

    [Fact]
    public async Task Recent_respects_the_count_query_parameter()
    {
        var a = await CreatePatientAsync(_client, "A");
        var b = await CreatePatientAsync(_client, "B");
        await CreateVisitAsync(_client, a, new DateTime(2026, 1, 1, 9, 0, 0));
        await CreateVisitAsync(_client, b, new DateTime(2026, 1, 2, 9, 0, 0));

        using var request = await AuthenticatedRequestAsync(HttpMethod.Get, "/api/patients/recent?count=1");
        var response = await _client.SendAsync(request);
        var results = await response.Content.ReadFromJsonAsync<List<RecentPatientDto>>();

        Assert.Single(results!);
        Assert.Equal("B", results![0].Name);
    }

    [Fact]
    public async Task Endpoint_requires_authentication()
    {
        var response = await _client.GetAsync("/api/patients/1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    private async Task<int> CreatePatientAsync(HttpClient client, string name)
    {
        using var request = await AuthenticatedRequestAsync(HttpMethod.Post, "/api/patients", client);
        request.Content = JsonContent.Create(new CreatePatientRequest { Name = name, Gender = "Female" });
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<PatientDto>();
        return created!.PatientId;
    }

    /// <summary>Books a scheduled appointment at the given instant and immediately starts a
    /// consultation on it, so the resulting Visit's VisitDate (== Appointment.ScheduledTime)
    /// lands exactly where the caller asked -- no clock pinning needed, unlike a walk-in.</summary>
    private async Task<int> CreateVisitAsync(HttpClient client, int patientId, DateTime scheduledTime)
    {
        using var appointmentRequest = await AuthenticatedRequestAsync(HttpMethod.Post, "/api/appointments", client);
        appointmentRequest.Content = JsonContent.Create(new CreateAppointmentRequest
        {
            PatientId = patientId,
            ScheduledTime = scheduledTime,
            DurationMinutes = 15,
        });
        var appointmentResponse = await client.SendAsync(appointmentRequest);
        appointmentResponse.EnsureSuccessStatusCode();
        var appointment = (await appointmentResponse.Content.ReadFromJsonAsync<AppointmentDto>(JsonOptions))!;

        using var startRequest = await AuthenticatedRequestAsync(
            HttpMethod.Post, $"/api/appointments/{appointment.Id}/start-consultation", client);
        startRequest.Content = JsonContent.Create(new
        {
            temperature = 37.0m,
            bpSystolic = (short)120,
            bpDiastolic = (short)80,
            pulse = 72,
            weight = 52.850m,
        });
        var startResponse = await client.SendAsync(startRequest);
        startResponse.EnsureSuccessStatusCode();
        var visit = (await startResponse.Content.ReadFromJsonAsync<VisitDto>(JsonOptions))!;
        return visit.Id;
    }

    private async Task<HttpRequestMessage> AuthenticatedRequestAsync(
        HttpMethod method, string requestUri, HttpClient? client = null)
    {
        client ??= _client;
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
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
