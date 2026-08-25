using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using PatientManagement.Api.Authentication;
using PatientManagement.Application.DTOs;

namespace PatientManagement.Api.Tests;

/// <summary>
/// Like AppointmentsControllerTests/PatientsControllerTests, each test gets
/// its own factory/database -- start-consultation mutates appointment state
/// and the "at most one visit per appointment" rule makes cross-test
/// leakage especially destructive.
/// </summary>
public class VisitsControllerTests : IDisposable
{
    private readonly AuthApiFactory _factory = new();
    private readonly HttpClient _client;

    public VisitsControllerTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose() => _factory.Dispose();

    private static readonly DateTime Slot9Am = new(2026, 4, 1, 9, 0, 0);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    private static object FullVitalsPayload(
        decimal temperature = 37.0m, short bpSystolic = 120, short bpDiastolic = 80,
        int pulse = 72, decimal weight = 52.850m, string? complaints = "Cough", string? diagnosis = "URI") => new
        {
            temperature,
            bpSystolic,
            bpDiastolic,
            pulse,
            weight,
            complaints,
            diagnosis,
        };

    [Fact]
    public async Task Starting_a_consultation_creates_a_visit_and_completes_the_appointment_on_the_daily_list()
    {
        var patientId = await CreatePatientAsync(_client, "Alice");
        var appointment = await PostAppointmentAsync(_client, patientId, Slot9Am, durationMinutes: 15);

        using var request = await AuthenticatedRequestAsync(
            HttpMethod.Post, $"/api/appointments/{appointment.Id}/start-consultation");
        request.Content = JsonContent.Create(FullVitalsPayload());
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var visit = await response.Content.ReadFromJsonAsync<VisitDto>(JsonOptions);
        Assert.NotNull(visit);
        Assert.Equal(appointment.Id, visit!.AppointmentId);
        Assert.Equal(patientId, visit.PatientId);
        Assert.Equal(1, visit.VisitNumber);
        Assert.Equal($"/api/visits/{visit.Id}", response.Headers.Location!.ToString());

        var daily = await GetDailyAsync(_client, DateOnly.FromDateTime(Slot9Am));
        var entry = Assert.Single(daily);
        Assert.Equal("Completed", entry.GetProperty("status").GetString());
        Assert.Equal(visit.Id, entry.GetProperty("visitId").GetInt32());
    }

    [Theory]
    [InlineData("temperature")]
    [InlineData("bpSystolic")]
    [InlineData("bpDiastolic")]
    [InlineData("pulse")]
    [InlineData("weight")]
    public async Task Starting_a_consultation_with_a_missing_vital_is_rejected_and_nothing_is_written(string omittedField)
    {
        var patientId = await CreatePatientAsync(_client, "Alice");
        var appointment = await PostAppointmentAsync(_client, patientId, Slot9Am, durationMinutes: 15);

        var payload = new Dictionary<string, object?>
        {
            ["temperature"] = 37.0m,
            ["bpSystolic"] = (short)120,
            ["bpDiastolic"] = (short)80,
            ["pulse"] = 72,
            ["weight"] = 52.850m,
            ["complaints"] = "Cough",
            ["diagnosis"] = "URI",
        };
        payload.Remove(omittedField);

        using var request = await AuthenticatedRequestAsync(
            HttpMethod.Post, $"/api/appointments/{appointment.Id}/start-consultation");
        request.Content = JsonContent.Create(payload);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Nothing was written -- confirmed two ways: the appointment is
        // still Scheduled (not Completed), and a follow-up full-vitals
        // attempt still gets visit number 1, not 2.
        var daily = await GetDailyAsync(_client, DateOnly.FromDateTime(Slot9Am));
        Assert.Equal("Scheduled", Assert.Single(daily).GetProperty("status").GetString());

        using var followUp = await AuthenticatedRequestAsync(
            HttpMethod.Post, $"/api/appointments/{appointment.Id}/start-consultation");
        followUp.Content = JsonContent.Create(FullVitalsPayload());
        var followUpResponse = await _client.SendAsync(followUp);
        var visit = await followUpResponse.Content.ReadFromJsonAsync<VisitDto>(JsonOptions);
        Assert.Equal(1, visit!.VisitNumber);
    }

    [Fact]
    public async Task Weight_round_trips_at_three_decimal_places_through_the_full_http_flow()
    {
        var patientId = await CreatePatientAsync(_client, "Alice");
        var appointment = await PostAppointmentAsync(_client, patientId, Slot9Am, durationMinutes: 15);

        using var request = await AuthenticatedRequestAsync(
            HttpMethod.Post, $"/api/appointments/{appointment.Id}/start-consultation");
        request.Content = JsonContent.Create(FullVitalsPayload(weight: 52.850m));
        var response = await _client.SendAsync(request);
        var created = await response.Content.ReadFromJsonAsync<VisitDto>(JsonOptions);

        var fetched = await GetVisitAsync(_client, created!.Id);
        Assert.Equal(52.850m, fetched.Weight);
    }

    [Fact]
    public async Task Starting_a_second_consultation_for_the_same_appointment_is_rejected()
    {
        var patientId = await CreatePatientAsync(_client, "Alice");
        var appointment = await PostAppointmentAsync(_client, patientId, Slot9Am, durationMinutes: 15);

        using var first = await AuthenticatedRequestAsync(
            HttpMethod.Post, $"/api/appointments/{appointment.Id}/start-consultation");
        first.Content = JsonContent.Create(FullVitalsPayload());
        (await _client.SendAsync(first)).EnsureSuccessStatusCode();

        using var second = await AuthenticatedRequestAsync(
            HttpMethod.Post, $"/api/appointments/{appointment.Id}/start-consultation");
        second.Content = JsonContent.Create(FullVitalsPayload());
        var response = await _client.SendAsync(second);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Starting_a_consultation_for_an_unknown_appointment_returns_not_found()
    {
        using var request = await AuthenticatedRequestAsync(HttpMethod.Post, "/api/appointments/4242/start-consultation");
        request.Content = JsonContent.Create(FullVitalsPayload());
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_returns_the_full_clinical_record_and_404_for_an_unknown_visit()
    {
        var patientId = await CreatePatientAsync(_client, "Alice");
        var appointment = await PostAppointmentAsync(_client, patientId, Slot9Am, durationMinutes: 15);
        using var start = await AuthenticatedRequestAsync(
            HttpMethod.Post, $"/api/appointments/{appointment.Id}/start-consultation");
        start.Content = JsonContent.Create(FullVitalsPayload(complaints: "Cough", diagnosis: "URI"));
        var startResponse = await _client.SendAsync(start);
        var created = await startResponse.Content.ReadFromJsonAsync<VisitDto>(JsonOptions);

        var fetched = await GetVisitAsync(_client, created!.Id);
        Assert.Equal("Cough", fetched.Complaints);
        Assert.Equal("URI", fetched.Diagnosis);

        using var missing = await AuthenticatedRequestAsync(HttpMethod.Get, "/api/visits/4242");
        Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(missing)).StatusCode);
    }

    [Fact]
    public async Task Update_changes_complaints_and_diagnosis_but_vitals_sent_in_the_body_are_ignored()
    {
        var patientId = await CreatePatientAsync(_client, "Alice");
        var appointment = await PostAppointmentAsync(_client, patientId, Slot9Am, durationMinutes: 15);
        using var start = await AuthenticatedRequestAsync(
            HttpMethod.Post, $"/api/appointments/{appointment.Id}/start-consultation");
        start.Content = JsonContent.Create(FullVitalsPayload(temperature: 37.2m, complaints: "Cough", diagnosis: "URI"));
        var startResponse = await _client.SendAsync(start);
        var created = await startResponse.Content.ReadFromJsonAsync<VisitDto>(JsonOptions);

        // UpdateVisitRequest has no vitals properties at all, so even a
        // client that tries to smuggle a vital through this endpoint has it
        // silently ignored by model binding -- vitals stay exactly what was
        // recorded when the consultation started.
        using var update = await AuthenticatedRequestAsync(HttpMethod.Put, $"/api/visits/{created!.Id}");
        update.Content = new StringContent(
            """{"complaints": "Cough, worse at night", "diagnosis": "Bronchitis", "temperature": 999}""",
            Encoding.UTF8,
            "application/json");
        var updateResponse = await _client.SendAsync(update);
        var updated = await updateResponse.Content.ReadFromJsonAsync<VisitDto>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Equal("Cough, worse at night", updated!.Complaints);
        Assert.Equal("Bronchitis", updated.Diagnosis);
        Assert.Equal(37.2m, updated.Temperature); // unchanged, not 999
    }

    [Fact]
    public async Task Update_for_an_unknown_visit_returns_not_found()
    {
        using var request = await AuthenticatedRequestAsync(HttpMethod.Put, "/api/visits/4242");
        request.Content = JsonContent.Create(new { complaints = "x", diagnosis = "y" });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Endpoints_require_authentication()
    {
        var start = await _client.PostAsJsonAsync("/api/appointments/1/start-consultation", FullVitalsPayload());
        var get = await _client.GetAsync("/api/visits/1");
        var put = await _client.PutAsJsonAsync("/api/visits/1", new { complaints = "x", diagnosis = "y" });

        Assert.Equal(HttpStatusCode.Unauthorized, start.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, get.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, put.StatusCode);
    }

    private async Task<int> CreatePatientAsync(HttpClient client, string name)
    {
        using var request = await AuthenticatedRequestAsync(HttpMethod.Post, "/api/patients", client);
        request.Content = JsonContent.Create(new CreatePatientRequest { Name = name, Gender = "Female" });
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<PatientDto>();
        return created!.PatientId;
    }

    private async Task<AppointmentDto> PostAppointmentAsync(
        HttpClient client, int patientId, DateTime scheduledTime, int durationMinutes)
    {
        using var request = await AuthenticatedRequestAsync(HttpMethod.Post, "/api/appointments", client);
        request.Content = JsonContent.Create(new CreateAppointmentRequest
        {
            PatientId = patientId,
            ScheduledTime = scheduledTime,
            DurationMinutes = durationMinutes,
        });
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AppointmentDto>(JsonOptions))!;
    }

    private async Task<VisitDto> GetVisitAsync(HttpClient client, int visitId)
    {
        using var request = await AuthenticatedRequestAsync(HttpMethod.Get, $"/api/visits/{visitId}", client);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<VisitDto>(JsonOptions))!;
    }

    private async Task<List<JsonElement>> GetDailyAsync(HttpClient client, DateOnly date)
    {
        using var request = await AuthenticatedRequestAsync(
            HttpMethod.Get, $"/api/appointments/daily?date={date:yyyy-MM-dd}", client);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<JsonElement>>(JsonOptions))!;
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
