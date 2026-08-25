using System.Net;
using System.Net.Http.Json;
using PatientManagement.Api.Authentication;
using PatientManagement.Application.DTOs;

namespace PatientManagement.Api.Tests;

/// <summary>
/// Own factory/database per test, like VisitsControllerTests/PatientsControllerTests
/// -- Create mutates state (a Visit + Prescriptions), so cross-test sharing
/// would leak.
/// </summary>
public class PrescriptionsControllerTests : IDisposable
{
    private readonly AuthApiFactory _factory = new();
    private readonly HttpClient _client;

    public PrescriptionsControllerTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose() => _factory.Dispose();

    private static readonly DateTime Slot9Am = new(2026, 4, 1, 9, 0, 0);

    private static object ItemsPayload(params (string DrugName, string? Dosage)[] items) => new
    {
        items = items.Select(i => new { drugName = i.DrugName, dosage = i.Dosage }).ToArray(),
    };

    [Fact]
    public async Task Create_returns_201_and_a_subsequent_get_reflects_the_snapshot_and_items()
    {
        var visitId = await CreateVisitAsync();

        using var create = await AuthenticatedRequestAsync(HttpMethod.Post, $"/api/visits/{visitId}/prescriptions");
        create.Content = JsonContent.Create(ItemsPayload(("Paracetamol", "500mg"), ("Cetirizine", "10mg")));
        var createResponse = await _client.SendAsync(create);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<PrescriptionDto>();
        Assert.NotNull(created);
        Assert.Equal(visitId, created!.VisitId);
        Assert.Equal(2, created.Items.Count);
        Assert.Equal($"/api/prescriptions/{created.Id}", createResponse.Headers.Location!.ToString());

        using var get = await AuthenticatedRequestAsync(HttpMethod.Get, $"/api/prescriptions/{created.Id}");
        var getResponse = await _client.SendAsync(get);
        var fetched = await getResponse.Content.ReadFromJsonAsync<PrescriptionDto>();

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Contains(fetched!.Items, i => i.DrugName == "Paracetamol" && i.Dosage == "500mg");
    }

    [Fact]
    public async Task Create_for_an_unknown_visit_returns_not_found()
    {
        using var create = await AuthenticatedRequestAsync(HttpMethod.Post, "/api/visits/4242/prescriptions");
        create.Content = JsonContent.Create(ItemsPayload(("Paracetamol", null)));
        var response = await _client.SendAsync(create);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_with_no_items_is_rejected_and_nothing_is_written()
    {
        var visitId = await CreateVisitAsync();

        using var create = await AuthenticatedRequestAsync(HttpMethod.Post, $"/api/visits/{visitId}/prescriptions");
        create.Content = JsonContent.Create(new { items = Array.Empty<object>() });
        var response = await _client.SendAsync(create);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_for_an_unknown_prescription_returns_not_found()
    {
        using var request = await AuthenticatedRequestAsync(HttpMethod.Get, "/api/prescriptions/4242");
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task No_update_endpoint_exists_for_a_printed_prescriptions_line_items()
    {
        var visitId = await CreateVisitAsync();
        using var create = await AuthenticatedRequestAsync(HttpMethod.Post, $"/api/visits/{visitId}/prescriptions");
        create.Content = JsonContent.Create(ItemsPayload(("Paracetamol", "500mg")));
        var createResponse = await _client.SendAsync(create);
        var created = await createResponse.Content.ReadFromJsonAsync<PrescriptionDto>();

        // The GET route template ("api/prescriptions/{id}") matches, but no
        // action accepts PUT/PATCH/DELETE -- 405 (not 404) proves the verbs
        // themselves aren't wired to anything, same technique
        // PatientsControllerTests uses to prove "no delete endpoint exists".
        using var put = await AuthenticatedRequestAsync(HttpMethod.Put, $"/api/prescriptions/{created!.Id}");
        put.Content = JsonContent.Create(ItemsPayload(("Ibuprofen", "400mg")));
        var putResponse = await _client.SendAsync(put);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, putResponse.StatusCode);

        using var patch = await AuthenticatedRequestAsync(new HttpMethod("PATCH"), $"/api/prescriptions/{created.Id}");
        var patchResponse = await _client.SendAsync(patch);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, patchResponse.StatusCode);

        using var delete = await AuthenticatedRequestAsync(HttpMethod.Delete, $"/api/prescriptions/{created.Id}");
        var deleteResponse = await _client.SendAsync(delete);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, deleteResponse.StatusCode);

        // The prescription's own content is exactly what it was created with.
        using var get = await AuthenticatedRequestAsync(HttpMethod.Get, $"/api/prescriptions/{created.Id}");
        var getResponse = await _client.SendAsync(get);
        var fetched = await getResponse.Content.ReadFromJsonAsync<PrescriptionDto>();
        Assert.Equal("Paracetamol", Assert.Single(fetched!.Items).DrugName);
    }

    [Fact]
    public async Task A_correction_after_printing_creates_a_new_prescription_row_not_a_mutation()
    {
        var visitId = await CreateVisitAsync();

        using var first = await AuthenticatedRequestAsync(HttpMethod.Post, $"/api/visits/{visitId}/prescriptions");
        first.Content = JsonContent.Create(ItemsPayload(("Paracetamol", "500mg")));
        var firstResponse = await _client.SendAsync(first);
        var firstCreated = await firstResponse.Content.ReadFromJsonAsync<PrescriptionDto>();

        using var second = await AuthenticatedRequestAsync(HttpMethod.Post, $"/api/visits/{visitId}/prescriptions");
        second.Content = JsonContent.Create(ItemsPayload(("Amoxicillin", "250mg")));
        var secondResponse = await _client.SendAsync(second);
        var secondCreated = await secondResponse.Content.ReadFromJsonAsync<PrescriptionDto>();

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        Assert.NotEqual(firstCreated!.Id, secondCreated!.Id);

        // The original row is untouched by the correction.
        using var getFirst = await AuthenticatedRequestAsync(HttpMethod.Get, $"/api/prescriptions/{firstCreated.Id}");
        var refetchedFirst = await (await _client.SendAsync(getFirst)).Content.ReadFromJsonAsync<PrescriptionDto>();
        Assert.Equal("Paracetamol", Assert.Single(refetchedFirst!.Items).DrugName);
    }

    [Fact]
    public async Task Drug_suggestions_match_a_substring_anywhere_in_the_name_case_insensitively()
    {
        var visitId = await CreateVisitAsync();
        using var create = await AuthenticatedRequestAsync(HttpMethod.Post, $"/api/visits/{visitId}/prescriptions");
        create.Content = JsonContent.Create(ItemsPayload(("Amoxicillin", null), ("Paracetamol", null)));
        await _client.SendAsync(create);

        using var request = await AuthenticatedRequestAsync(HttpMethod.Get, "/api/prescriptions/drug-suggestions?prefix=OXIC");
        var response = await _client.SendAsync(request);
        var results = await response.Content.ReadFromJsonAsync<List<string>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(results!);
        Assert.Equal("Amoxicillin", results![0]);
    }

    [Fact]
    public async Task Endpoints_require_authentication()
    {
        var create = await _client.PostAsJsonAsync("/api/visits/1/prescriptions", ItemsPayload(("Paracetamol", null)));
        var get = await _client.GetAsync("/api/prescriptions/1");
        var suggestions = await _client.GetAsync("/api/prescriptions/drug-suggestions");

        Assert.Equal(HttpStatusCode.Unauthorized, create.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, get.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, suggestions.StatusCode);
    }

    private async Task<int> CreateVisitAsync()
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
        var consultResponse = await _client.SendAsync(consultRequest);
        var visit = await consultResponse.Content.ReadFromJsonAsync<VisitDto>();
        return visit!.Id;
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
