using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using PatientManagement.Api.Authentication;
using PatientManagement.Application.DTOs;
using PatientManagement.Domain.Models;

namespace PatientManagement.Api.Tests;

/// <summary>Fixed-instant clock so a walk-in can be forced onto a slot the test chose.</summary>
file sealed class FixedTimeProvider(DateTime localNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new DateTimeOffset(localNow).ToUniversalTime();
}

/// <summary>
/// Like PatientsControllerTests, each test gets its own factory/database --
/// POST/PUT here mutate appointment state and the unique-slot rule makes
/// cross-test leakage especially destructive.
/// </summary>
public class AppointmentsControllerTests : IDisposable
{
    private readonly AuthApiFactory _factory = new();
    private readonly HttpClient _client;

    public AppointmentsControllerTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose() => _factory.Dispose();

    private static readonly DateTime Slot9Am = new(2026, 3, 2, 9, 0, 0);
    private static readonly DateOnly Day = new(2026, 3, 2);

    [Fact]
    public async Task Scheduled_appointment_persists_the_duration_the_doctor_entered()
    {
        var patientId = await CreatePatientAsync(_client, "Alice");

        var created = await PostAppointmentAsync(_client, patientId, Slot9Am, durationMinutes: 45);
        var second = await PostAppointmentAsync(_client, patientId, Slot9Am.AddHours(2), durationMinutes: 7);

        Assert.Equal(45, created.DurationMinutes);
        Assert.Equal(7, second.DurationMinutes);

        // And it survives the round-trip out of the database, too.
        var daily = await GetDailyAsync(_client, Day);
        Assert.Equal(new[] { 45, 7 }, daily.Select(a => a.DurationMinutes).ToArray());
    }

    [Fact]
    public async Task Scheduled_appointment_without_a_duration_is_rejected_rather_than_defaulted()
    {
        var patientId = await CreatePatientAsync(_client, "Alice");

        // durationMinutes deliberately absent from the payload entirely.
        using var request = await AuthenticatedRequestAsync(HttpMethod.Post, "/api/appointments");
        request.Content = new StringContent(
            $$"""{"patientId": {{patientId}}, "scheduledTime": "2026-03-02T09:00:00"}""",
            Encoding.UTF8,
            "application/json");
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Nothing was written -- no appointment was created with a silent default.
        var daily = await GetDailyAsync(_client, Day);
        Assert.Empty(daily);
    }

    [Fact]
    public async Task Second_scheduled_appointment_in_the_same_slot_is_rejected()
    {
        var alice = await CreatePatientAsync(_client, "Alice");
        var bob = await CreatePatientAsync(_client, "Bob");

        await PostAppointmentAsync(_client, alice, Slot9Am, durationMinutes: 15);

        using var request = await AuthenticatedRequestAsync(HttpMethod.Post, "/api/appointments");
        request.Content = JsonContent.Create(new CreateAppointmentRequest
        {
            PatientId = bob,
            ScheduledTime = Slot9Am,
            DurationMinutes = 30,
        });
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Single(await GetDailyAsync(_client, Day));
    }

    [Fact]
    public async Task Walk_in_creates_exactly_one_appointment_and_one_linked_visit()
    {
        var patientId = await CreatePatientAsync(_client, "Alice");

        var walkIn = await PostWalkInAsync(_client, patientId);

        Assert.NotEqual(0, walkIn.AppointmentId);
        Assert.NotEqual(0, walkIn.VisitId);
        Assert.Equal(patientId, walkIn.PatientId);
        Assert.Equal(1, walkIn.VisitNumber); // first visit for this patient

        var daily = await GetDailyAsync(_client, DateOnly.FromDateTime(DateTime.Today));
        var entry = Assert.Single(daily);
        Assert.Equal(walkIn.AppointmentId, entry.Id);
        Assert.Equal(walkIn.VisitId, entry.VisitId); // the visit is linked to that appointment
    }

    [Fact]
    public async Task Walk_in_landing_on_an_already_booked_slot_is_rejected()
    {
        // Pin the clock so the walk-in lands exactly on the slot booked below.
        var now = new DateTime(2026, 3, 2, 10, 15, 0, DateTimeKind.Local);
        using var factory = new AuthApiFactory { Clock = new FixedTimeProvider(now) };
        using var client = factory.CreateClient();

        var alice = await CreatePatientAsync(client, "Alice");
        var bob = await CreatePatientAsync(client, "Bob");
        await PostAppointmentAsync(client, alice, now, durationMinutes: 15);

        using var request = await AuthenticatedRequestAsync(HttpMethod.Post, "/api/appointments/walk-in", client);
        request.Content = JsonContent.Create(WalkInPayload(bob));
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        // The scheduled appointment is still the only one on the day.
        var daily = await GetDailyAsync(client, DateOnly.FromDateTime(now));
        var entry = Assert.Single(daily);
        Assert.Null(entry.VisitId);
    }

    [Fact]
    public async Task Daily_list_returns_scheduled_and_walk_in_entries_together_ordered_by_time()
    {
        var today = DateTime.Now;
        var alice = await CreatePatientAsync(_client, "Alice");
        var bob = await CreatePatientAsync(_client, "Bob");

        // Two scheduled appointments bracketing "now", plus a walk-in at "now".
        await PostAppointmentAsync(_client, alice, today.Date.AddHours(1), durationMinutes: 15);
        await PostAppointmentAsync(_client, bob, today.Date.AddHours(23), durationMinutes: 15);
        var walkIn = await PostWalkInAsync(_client, alice);

        var daily = await GetDailyAsync(_client, DateOnly.FromDateTime(today));

        Assert.Equal(3, daily.Count);
        Assert.Equal(
            daily.Select(a => a.ScheduledTime).OrderBy(t => t).ToArray(),
            daily.Select(a => a.ScheduledTime).ToArray());
        // The walk-in is one of the entries in the same list, not a separate feed.
        Assert.Contains(daily, a => a.Id == walkIn.AppointmentId && a.VisitId == walkIn.VisitId);
    }

    [Theory]
    [InlineData("Cancelled")]
    [InlineData("NoShow")]
    public async Task Status_can_be_updated_to_cancelled_or_no_show(string status)
    {
        var patientId = await CreatePatientAsync(_client, "Alice");
        var created = await PostAppointmentAsync(_client, patientId, Slot9Am, durationMinutes: 15);

        using var request = await AuthenticatedRequestAsync(HttpMethod.Put, $"/api/appointments/{created.Id}/status");
        request.Content = new StringContent($$"""{"status": "{{status}}"}""", Encoding.UTF8, "application/json");
        var response = await _client.SendAsync(request);
        var updated = await response.Content.ReadFromJsonAsync<AppointmentDto>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Enum.Parse<AppointmentStatus>(status), updated!.Status);
    }

    /// <summary>
    /// FLAGGED ASSUMPTION (see docs/implementation-progress.md Step 12): setting
    /// Completed by hand is rejected -- completion is only ever a side effect of
    /// creating a visit. This rule was a plan-review recommendation that was
    /// never locked into implementation-brd.md; if it is overturned, this test
    /// and the guard in AppointmentService are what to delete.
    /// </summary>
    [Fact]
    public async Task Status_cannot_be_set_to_completed_by_hand()
    {
        var patientId = await CreatePatientAsync(_client, "Alice");
        var created = await PostAppointmentAsync(_client, patientId, Slot9Am, durationMinutes: 15);

        using var request = await AuthenticatedRequestAsync(HttpMethod.Put, $"/api/appointments/{created.Id}/status");
        request.Content = new StringContent("""{"status": "Completed"}""", Encoding.UTF8, "application/json");
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Completed", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var daily = await GetDailyAsync(_client, Day);
        Assert.Equal(AppointmentStatus.Scheduled, Assert.Single(daily).Status);
    }

    [Fact]
    public async Task Status_update_for_an_unknown_appointment_returns_not_found()
    {
        using var request = await AuthenticatedRequestAsync(HttpMethod.Put, "/api/appointments/4242/status");
        request.Content = new StringContent("""{"status": "Cancelled"}""", Encoding.UTF8, "application/json");
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Daily_list_reports_HasPrescription_only_once_a_prescription_exists_for_the_visit()
    {
        var alice = await CreatePatientAsync(_client, "Alice");
        var bob = await CreatePatientAsync(_client, "Bob");

        var withRx = await PostWalkInAsync(_client, alice);
        var withoutRx = await PostWalkInAsync(_client, bob);

        using var createRx = await AuthenticatedRequestAsync(HttpMethod.Post, $"/api/visits/{withRx.VisitId}/prescriptions");
        createRx.Content = JsonContent.Create(new { items = new[] { new { drugName = "Paracetamol", dosage = "500mg" } } });
        (await _client.SendAsync(createRx)).EnsureSuccessStatusCode();

        var daily = await GetDailyAsync(_client, DateOnly.FromDateTime(DateTime.Today));

        Assert.True(daily.Single(a => a.Id == withRx.AppointmentId).HasPrescription);
        Assert.False(daily.Single(a => a.Id == withoutRx.AppointmentId).HasPrescription);
    }

    [Fact]
    public async Task Endpoints_require_authentication()
    {
        var daily = await _client.GetAsync("/api/appointments/daily?date=2026-03-02");
        var create = await _client.PostAsJsonAsync("/api/appointments", new CreateAppointmentRequest
        {
            PatientId = 0,
            ScheduledTime = Slot9Am,
            DurationMinutes = 15,
        });

        Assert.Equal(HttpStatusCode.Unauthorized, daily.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, create.StatusCode);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    private static object WalkInPayload(int patientId) => new
    {
        patientId,
        durationMinutes = 12,
        temperature = 37.0m,
        bpSystolic = (short)120,
        bpDiastolic = (short)80,
        pulse = 72,
        weight = 52.850m,
        complaints = "Cough",
        diagnosis = "URI",
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

    private async Task<WalkInVisitDto> PostWalkInAsync(HttpClient client, int patientId)
    {
        using var request = await AuthenticatedRequestAsync(HttpMethod.Post, "/api/appointments/walk-in", client);
        request.Content = JsonContent.Create(WalkInPayload(patientId));
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<WalkInVisitDto>(JsonOptions))!;
    }

    private async Task<List<AppointmentDto>> GetDailyAsync(HttpClient client, DateOnly date)
    {
        using var request = await AuthenticatedRequestAsync(
            HttpMethod.Get, $"/api/appointments/daily?date={date:yyyy-MM-dd}", client);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<AppointmentDto>>(JsonOptions))!;
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
