using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PatientManagement.Api.Authentication;
using PatientManagement.Application.DTOs;

namespace PatientManagement.Api.Tests;

/// <summary>
/// Own factory/database per test, like VisitsControllerTests/PrescriptionsControllerTests
/// -- read-only from this controller's own point of view, but the fixture
/// data (patient/appointment/visit/prescription) it sets up each time would
/// otherwise leak across tests.
/// </summary>
public class PatientHistoryControllerTests : IDisposable
{
    private readonly AuthApiFactory _factory = new();
    private readonly HttpClient _client;

    public PatientHistoryControllerTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose() => _factory.Dispose();

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    [Fact]
    public async Task GetVisits_returns_visits_newest_first_and_the_date_range_filter_is_inclusive()
    {
        var patientId = await CreatePatientAsync();
        var earlyVisit = await CreateConsultationVisitAsync(patientId, new DateTime(2026, 1, 10, 9, 0, 0), diagnosis: "Early");
        var lateVisit = await CreateConsultationVisitAsync(patientId, new DateTime(2026, 3, 20, 9, 0, 0), diagnosis: "Late");

        // Unfiltered: both visits, newest first.
        using var all = await AuthenticatedRequestAsync(HttpMethod.Get, $"/api/patients/{patientId}/visits");
        var allResponse = await _client.SendAsync(all);
        var allResult = await allResponse.Content.ReadFromJsonAsync<List<VisitSummaryDto>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, allResponse.StatusCode);
        Assert.Equal(2, allResult!.Count);
        Assert.Equal("Late", allResult[0].Diagnosis);
        Assert.Equal("Early", allResult[1].Diagnosis);

        // Range covering only the early visit's day, inclusive.
        using var ranged = await AuthenticatedRequestAsync(
            HttpMethod.Get, $"/api/patients/{patientId}/visits?from=2026-01-10&to=2026-01-10");
        var rangedResponse = await _client.SendAsync(ranged);
        var rangedResult = await rangedResponse.Content.ReadFromJsonAsync<List<VisitSummaryDto>>(JsonOptions);

        Assert.Single(rangedResult!);
        Assert.Equal(earlyVisit.VisitId, rangedResult![0].Id);
    }

    [Fact]
    public async Task GetVisits_for_a_patient_with_no_visits_returns_an_empty_list()
    {
        var patientId = await CreatePatientAsync();

        using var request = await AuthenticatedRequestAsync(HttpMethod.Get, $"/api/patients/{patientId}/visits");
        var response = await _client.SendAsync(request);
        var result = await response.Content.ReadFromJsonAsync<List<VisitSummaryDto>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(result!);
    }

    [Fact]
    public async Task GetVisitDetail_via_shared_GET_visits_endpoint_returns_vitals_complaints_diagnosis_and_prescriptions()
    {
        var patientId = await CreatePatientAsync();
        var visit = await CreateConsultationVisitAsync(patientId, new DateTime(2026, 4, 1, 9, 0, 0), diagnosis: "URI");

        using var prescribe = await AuthenticatedRequestAsync(HttpMethod.Post, $"/api/visits/{visit.VisitId}/prescriptions");
        prescribe.Content = JsonContent.Create(new { items = new[] { new { drugName = "Paracetamol", dosage = "500mg" } } });
        (await _client.SendAsync(prescribe)).EnsureSuccessStatusCode();

        using var detail = await AuthenticatedRequestAsync(HttpMethod.Get, $"/api/visits/{visit.VisitId}");
        var detailResponse = await _client.SendAsync(detail);
        var result = await detailResponse.Content.ReadFromJsonAsync<VisitDetailDto>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        Assert.Equal(visit.VisitId, result!.Id);
        Assert.Equal(new DateTime(2026, 4, 1, 9, 0, 0), result.VisitDate);
        Assert.Equal(37.0m, result.Temperature);
        Assert.Equal("URI", result.Diagnosis);
        Assert.Single(result.Prescriptions);
        Assert.Equal("Paracetamol", result.Prescriptions[0].Items[0].DrugName);
    }

    [Fact]
    public async Task GetVisitDetail_for_an_unknown_visit_returns_not_found()
    {
        using var request = await AuthenticatedRequestAsync(HttpMethod.Get, "/api/visits/4242");
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Endpoints_require_authentication()
    {
        var list = await _client.GetAsync("/api/patients/1/visits");
        var detail = await _client.GetAsync("/api/visits/1");

        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, detail.StatusCode);
    }

    private async Task<int> CreatePatientAsync()
    {
        using var request = await AuthenticatedRequestAsync(HttpMethod.Post, "/api/patients");
        request.Content = JsonContent.Create(new CreatePatientRequest { Name = "Alice", Gender = "Female" });
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<PatientDto>();
        return created!.PatientId;
    }

    private async Task<(int AppointmentId, int VisitId)> CreateConsultationVisitAsync(
        int patientId, DateTime scheduledTime, string diagnosis)
    {
        using var appointmentRequest = await AuthenticatedRequestAsync(HttpMethod.Post, "/api/appointments");
        appointmentRequest.Content = JsonContent.Create(new CreateAppointmentRequest
        {
            PatientId = patientId,
            ScheduledTime = scheduledTime,
            DurationMinutes = 15,
        });
        var appointmentResponse = await _client.SendAsync(appointmentRequest);
        appointmentResponse.EnsureSuccessStatusCode();
        var appointment = (await appointmentResponse.Content.ReadFromJsonAsync<AppointmentDto>(JsonOptions))!;

        using var startRequest = await AuthenticatedRequestAsync(
            HttpMethod.Post, $"/api/appointments/{appointment.Id}/start-consultation");
        startRequest.Content = JsonContent.Create(new
        {
            temperature = 37.0m,
            bpSystolic = (short)120,
            bpDiastolic = (short)80,
            pulse = 72,
            weight = 52.850m,
            complaints = "Cough",
            diagnosis,
        });
        var startResponse = await _client.SendAsync(startRequest);
        startResponse.EnsureSuccessStatusCode();
        var visit = (await startResponse.Content.ReadFromJsonAsync<VisitDto>(JsonOptions))!;

        return (appointment.Id, visit.Id);
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
