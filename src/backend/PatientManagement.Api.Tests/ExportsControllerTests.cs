using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using PatientManagement.Api.Authentication;
using PatientManagement.Application.DTOs;

namespace PatientManagement.Api.Tests;

/// <summary>
/// Own factory/database per test, like PrescriptionsControllerTests/VisitsControllerTests
/// -- exports mutate the ExportAuditLog table, so cross-test sharing would leak.
/// </summary>
public class ExportsControllerTests : IDisposable
{
    private readonly AuthApiFactory _factory = new();
    private readonly HttpClient _client;

    public ExportsControllerTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose() => _factory.Dispose();

    private static readonly DateTime Slot9Am = new(2026, 4, 1, 9, 0, 0);

    [Fact]
    public async Task ExportCsv_with_a_selected_patient_scope_returns_a_zip_with_both_files()
    {
        var patientId = await CreatePatientWithVisitAsync();

        using var request = await AuthenticatedRequestAsync(HttpMethod.Post, "/api/exports/csv");
        request.Content = JsonContent.Create(new { scope = new { patientIds = new[] { patientId } }, confirmed = true });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/zip", response.Content.Headers.ContentType!.MediaType);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        using var stream = new MemoryStream(bytes);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        Assert.Contains(zip.Entries, e => e.Name == "patients.csv");
        Assert.Contains(zip.Entries, e => e.Name == "visits.csv");
    }

    [Fact]
    public async Task ExportCsv_with_no_scope_is_rejected_and_nothing_is_exported()
    {
        using var request = await AuthenticatedRequestAsync(HttpMethod.Post, "/api/exports/csv");
        request.Content = JsonContent.Create(new { scope = new { }, confirmed = true });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ExportCsv_without_confirmation_is_rejected()
    {
        var patientId = await CreatePatientWithVisitAsync();

        using var request = await AuthenticatedRequestAsync(HttpMethod.Post, "/api/exports/csv");
        request.Content = JsonContent.Create(new { scope = new { patientIds = new[] { patientId } }, confirmed = false });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ExportCsv_by_date_range_succeeds()
    {
        await CreatePatientWithVisitAsync();

        using var request = await AuthenticatedRequestAsync(HttpMethod.Post, "/api/exports/csv");
        request.Content = JsonContent.Create(new
        {
            scope = new { dateFrom = "2026-01-01", dateTo = "2026-12-31" },
            confirmed = true,
        });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ExportPdf_for_a_known_patient_returns_a_pdf()
    {
        var patientId = await CreatePatientWithVisitAsync();

        using var request = await AuthenticatedRequestAsync(HttpMethod.Post, "/api/exports/pdf");
        request.Content = JsonContent.Create(new { patientId, confirmed = true });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType!.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 0);
    }

    [Fact]
    public async Task ExportPdf_for_an_unknown_patient_returns_not_found()
    {
        using var request = await AuthenticatedRequestAsync(HttpMethod.Post, "/api/exports/pdf");
        request.Content = JsonContent.Create(new { patientId = 4242, confirmed = true });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ExportPdf_without_confirmation_is_rejected()
    {
        var patientId = await CreatePatientWithVisitAsync();

        using var request = await AuthenticatedRequestAsync(HttpMethod.Post, "/api/exports/pdf");
        request.Content = JsonContent.Create(new { patientId, confirmed = false });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_completed_export_is_reflected_in_the_audit_log()
    {
        var patientId = await CreatePatientWithVisitAsync();

        using var export = await AuthenticatedRequestAsync(HttpMethod.Post, "/api/exports/csv");
        export.Content = JsonContent.Create(new { scope = new { patientIds = new[] { patientId } }, confirmed = true });
        (await _client.SendAsync(export)).EnsureSuccessStatusCode();

        using var auditRequest = await AuthenticatedRequestAsync(HttpMethod.Get, "/api/exports/audit-log");
        var auditResponse = await _client.SendAsync(auditRequest);
        var log = await auditResponse.Content.ReadFromJsonAsync<List<ExportAuditLogDto>>();

        Assert.Equal(HttpStatusCode.OK, auditResponse.StatusCode);
        Assert.Single(log!);
        Assert.Equal("Csv", log![0].Format);
        Assert.Equal(AuthApiFactory.SeedUsername, log[0].Username);
    }

    [Fact]
    public async Task Endpoints_require_authentication()
    {
        var csv = await _client.PostAsJsonAsync("/api/exports/csv", new { scope = new { patientIds = new[] { 1 } }, confirmed = true });
        var pdf = await _client.PostAsJsonAsync("/api/exports/pdf", new { patientId = 1, confirmed = true });
        var auditLog = await _client.GetAsync("/api/exports/audit-log");

        Assert.Equal(HttpStatusCode.Unauthorized, csv.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, pdf.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, auditLog.StatusCode);
    }

    private async Task<int> CreatePatientWithVisitAsync()
    {
        using var patientRequest = await AuthenticatedRequestAsync(HttpMethod.Post, "/api/patients");
        patientRequest.Content = JsonContent.Create(new CreatePatientRequest { Name = "Alice", Gender = "Female" });
        var patientResponse = await _client.SendAsync(patientRequest);
        var patient = await patientResponse.Content.ReadFromJsonAsync<PatientDto>();

        using var appointmentRequest = await AuthenticatedRequestAsync(HttpMethod.Post, "/api/appointments");
        appointmentRequest.Content = JsonContent.Create(new CreateAppointmentRequest
        {
            PatientId = patient!.PatientId,
            ScheduledTime = Slot9Am,
            DurationMinutes = 15,
        });
        var appointmentResponse = await _client.SendAsync(appointmentRequest);
        var appointment = await appointmentResponse.Content.ReadFromJsonAsync<AppointmentDto>(
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)
            {
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
            });

        using var consultRequest = await AuthenticatedRequestAsync(
            HttpMethod.Post, $"/api/appointments/{appointment!.Id}/start-consultation");
        consultRequest.Content = JsonContent.Create(new
        {
            temperature = 37.0m,
            bpSystolic = (short)120,
            bpDiastolic = (short)80,
            pulse = 72,
            weight = 52.850m,
            complaints = "Cough",
            diagnosis = "URI",
        });
        (await _client.SendAsync(consultRequest)).EnsureSuccessStatusCode();

        return patient.PatientId;
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
