using Microsoft.EntityFrameworkCore;
using PatientManagement.Application.DTOs;
using PatientManagement.Application.Services;
using PatientManagement.Domain.Models;
using PatientManagement.Infrastructure.Data;
using PatientManagement.Infrastructure.Services;
using Xunit;

namespace PatientManagement.Infrastructure.Tests.Services;

public class ConsultationServiceTests
{
    private static async Task<(AppDbContext Db, Func<Task> Cleanup)> CreateFreshDatabaseAsync(string dbName)
    {
        var connectionString = $"Server=(localdb)\\MSSQLLocalDB;Database={dbName};Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        var db = new AppDbContext(options);
        await db.Database.MigrateAsync();

        return (db, async () =>
        {
            await db.Database.EnsureDeletedAsync();
            await db.DisposeAsync();
        });
    }

    private static StartConsultationRequest MakeRequest(
        decimal? temperature = 37.0m,
        short? bpSystolic = 120,
        short? bpDiastolic = 80,
        int? pulse = 72,
        decimal? weight = 52.850m,
        string? complaints = "Cough",
        string? diagnosis = "URI") => new()
    {
        Temperature = temperature,
        BpSystolic = bpSystolic,
        BpDiastolic = bpDiastolic,
        Pulse = pulse,
        Weight = weight,
        Complaints = complaints,
        Diagnosis = diagnosis,
    };

    [Fact]
    public async Task Starting_a_consultation_creates_a_linked_visit_and_completes_the_appointment()
    {
        var (db, cleanup) = await CreateFreshDatabaseAsync($"PatientManagement_Consult_{Guid.NewGuid():N}");
        try
        {
            var patient = new Patient { Name = "Alice", Gender = "Female" };
            db.Patients.Add(patient);
            await db.SaveChangesAsync();

            var appointment = new Appointment
            {
                PatientId = patient.PatientId,
                ScheduledTime = new DateTime(2026, 4, 1, 9, 0, 0),
                DurationMinutes = 15,
                Status = AppointmentStatus.Scheduled,
            };
            db.Appointments.Add(appointment);
            await db.SaveChangesAsync();

            var service = new ConsultationService(db);
            var result = await service.StartConsultationAsync(appointment.Id, MakeRequest());

            Assert.NotNull(result);
            Assert.Equal(appointment.Id, result!.AppointmentId);
            Assert.Equal(patient.PatientId, result.PatientId);
            Assert.Equal(1, result.VisitNumber);

            var reloadedAppointment = await db.Appointments.AsNoTracking().SingleAsync(a => a.Id == appointment.Id);
            Assert.Equal(AppointmentStatus.Completed, reloadedAppointment.Status);
        }
        finally
        {
            await cleanup();
        }
    }

    [Fact]
    public async Task Visit_numbering_continues_across_the_walk_in_and_scheduled_paths_for_the_same_patient()
    {
        var (db, cleanup) = await CreateFreshDatabaseAsync($"PatientManagement_ConsultNum_{Guid.NewGuid():N}");
        try
        {
            var patient = new Patient { Name = "Alice", Gender = "Female" };
            db.Patients.Add(patient);
            await db.SaveChangesAsync();

            // Visit #1 via the walk-in path.
            var walkInService = new WalkInService(db, new FixedTimeProvider(DateTimeOffset.Now));
            var walkInVisit = await walkInService.CreateWalkInVisitAsync(new WalkInVisitRequest(
                patient.PatientId, DurationMinutes: 10, Temperature: 37.0m, BpSystolic: 118,
                BpDiastolic: 76, Pulse: 70, Weight: 50.0m, Complaints: null, Diagnosis: null));
            Assert.Equal(1, walkInVisit.VisitNumber);

            // Visit #2 via the scheduled-consultation path -- numbering must
            // pick up from the walk-in path's own visit, not reset to 1.
            var scheduledAppointment = new Appointment
            {
                PatientId = patient.PatientId,
                ScheduledTime = new DateTime(2026, 4, 1, 9, 0, 0),
                DurationMinutes = 15,
                Status = AppointmentStatus.Scheduled,
            };
            db.Appointments.Add(scheduledAppointment);
            await db.SaveChangesAsync();

            var consultationService = new ConsultationService(db);
            var visit2 = await consultationService.StartConsultationAsync(scheduledAppointment.Id, MakeRequest());

            Assert.Equal(2, visit2!.VisitNumber);
        }
        finally
        {
            await cleanup();
        }
    }

    [Fact]
    public async Task Starting_a_consultation_twice_for_the_same_appointment_is_rejected()
    {
        var (db, cleanup) = await CreateFreshDatabaseAsync($"PatientManagement_ConsultTwice_{Guid.NewGuid():N}");
        try
        {
            var patient = new Patient { Name = "Alice", Gender = "Female" };
            db.Patients.Add(patient);
            await db.SaveChangesAsync();
            var appointment = new Appointment
            {
                PatientId = patient.PatientId,
                ScheduledTime = new DateTime(2026, 4, 1, 9, 0, 0),
                DurationMinutes = 15,
                Status = AppointmentStatus.Scheduled,
            };
            db.Appointments.Add(appointment);
            await db.SaveChangesAsync();

            var service = new ConsultationService(db);
            await service.StartConsultationAsync(appointment.Id, MakeRequest());

            await Assert.ThrowsAsync<ConsultationAlreadyStartedException>(() =>
                service.StartConsultationAsync(appointment.Id, MakeRequest()));

            Assert.Single(db.Visits); // the rejected second attempt inserted nothing
        }
        finally
        {
            await cleanup();
        }
    }

    [Fact]
    public async Task Starting_a_consultation_for_an_unknown_appointment_returns_null()
    {
        var (db, cleanup) = await CreateFreshDatabaseAsync($"PatientManagement_ConsultUnknown_{Guid.NewGuid():N}");
        try
        {
            var service = new ConsultationService(db);
            var result = await service.StartConsultationAsync(4242, MakeRequest());

            Assert.Null(result);
            Assert.Empty(db.Visits);
        }
        finally
        {
            await cleanup();
        }
    }

    [Fact]
    public async Task Weight_round_trips_at_three_decimal_places_without_rounding()
    {
        var (db, cleanup) = await CreateFreshDatabaseAsync($"PatientManagement_ConsultWeight_{Guid.NewGuid():N}");
        try
        {
            var patient = new Patient { Name = "Alice", Gender = "Female" };
            db.Patients.Add(patient);
            await db.SaveChangesAsync();
            var appointment = new Appointment
            {
                PatientId = patient.PatientId,
                ScheduledTime = new DateTime(2026, 4, 1, 9, 0, 0),
                DurationMinutes = 15,
                Status = AppointmentStatus.Scheduled,
            };
            db.Appointments.Add(appointment);
            await db.SaveChangesAsync();

            var service = new ConsultationService(db);
            var result = await service.StartConsultationAsync(appointment.Id, MakeRequest(weight: 52.850m));

            var reloaded = await db.Visits.AsNoTracking().SingleAsync(v => v.Id == result!.Id);
            Assert.Equal(52.850m, reloaded.Weight);
        }
        finally
        {
            await cleanup();
        }
    }

    [Fact]
    public async Task Update_changes_complaints_and_diagnosis_but_leaves_vitals_untouched()
    {
        var (db, cleanup) = await CreateFreshDatabaseAsync($"PatientManagement_ConsultUpdate_{Guid.NewGuid():N}");
        try
        {
            var patient = new Patient { Name = "Alice", Gender = "Female" };
            db.Patients.Add(patient);
            await db.SaveChangesAsync();
            var appointment = new Appointment
            {
                PatientId = patient.PatientId,
                ScheduledTime = new DateTime(2026, 4, 1, 9, 0, 0),
                DurationMinutes = 15,
                Status = AppointmentStatus.Scheduled,
            };
            db.Appointments.Add(appointment);
            await db.SaveChangesAsync();

            var service = new ConsultationService(db);
            var created = await service.StartConsultationAsync(
                appointment.Id, MakeRequest(temperature: 37.2m, weight: 52.850m, complaints: "Cough", diagnosis: "URI"));

            var updated = await service.UpdateAsync(created!.Id, new UpdateVisitRequest("Cough, worse at night", "Bronchitis"));

            Assert.NotNull(updated);
            Assert.Equal("Cough, worse at night", updated!.Complaints);
            Assert.Equal("Bronchitis", updated.Diagnosis);
            // Vitals are exactly what was recorded at consultation start.
            Assert.Equal(37.2m, updated.Temperature);
            Assert.Equal(52.850m, updated.Weight);
        }
        finally
        {
            await cleanup();
        }
    }

    [Fact]
    public async Task Update_for_an_unknown_visit_returns_null()
    {
        var (db, cleanup) = await CreateFreshDatabaseAsync($"PatientManagement_ConsultUpdateUnknown_{Guid.NewGuid():N}");
        try
        {
            var service = new ConsultationService(db);
            var result = await service.UpdateAsync(4242, new UpdateVisitRequest("x", "y"));

            Assert.Null(result);
        }
        finally
        {
            await cleanup();
        }
    }

    [Fact]
    public async Task Get_for_an_unknown_visit_returns_null()
    {
        var (db, cleanup) = await CreateFreshDatabaseAsync($"PatientManagement_ConsultGetUnknown_{Guid.NewGuid():N}");
        try
        {
            var service = new ConsultationService(db);
            var result = await service.GetAsync(4242);

            Assert.Null(result);
        }
        finally
        {
            await cleanup();
        }
    }
}

/// <summary>Fixed-instant clock so the walk-in leg of the numbering test lands deterministically.</summary>
file sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
