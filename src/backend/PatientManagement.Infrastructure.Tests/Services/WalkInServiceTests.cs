using Microsoft.EntityFrameworkCore;
using PatientManagement.Application.DTOs;
using PatientManagement.Application.Services;
using PatientManagement.Domain.Models;
using PatientManagement.Infrastructure.Data;
using PatientManagement.Infrastructure.Services;
using Xunit;

namespace PatientManagement.Infrastructure.Tests.Services;

/// <summary>Fixed-instant clock so two calls can be forced to land "at the same moment".</summary>
file sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

public class WalkInServiceTests
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

    private static WalkInVisitRequest MakeRequest(int patientId) =>
        new(patientId, DurationMinutes: 15, Temperature: 37.0m, BpSystolic: 120, BpDiastolic: 80,
            Pulse: 72, Weight: 52.850m, Complaints: "Cough", Diagnosis: "URI");

    [Fact]
    public async Task Creates_exactly_one_completed_appointment_and_one_linked_visit()
    {
        var (db, cleanup) = await CreateFreshDatabaseAsync($"PatientManagement_WalkIn_{Guid.NewGuid():N}");
        try
        {
            var patient = new Patient { Name = "Alice", Gender = "Female" };
            db.Patients.Add(patient);
            await db.SaveChangesAsync();

            var service = new WalkInService(db, new FixedTimeProvider(DateTimeOffset.Now));
            var visit = await service.CreateWalkInVisitAsync(MakeRequest(patient.PatientId));

            Assert.Single(db.Appointments);
            Assert.Single(db.Visits);
            var appointment = await db.Appointments.SingleAsync();
            Assert.Equal(AppointmentStatus.Completed, appointment.Status);
            Assert.Equal(appointment.Id, visit.AppointmentId);
            Assert.Equal(patient.PatientId, visit.PatientId);
        }
        finally
        {
            await cleanup();
        }
    }

    [Fact]
    public async Task Visit_numbers_increment_per_patient_not_globally()
    {
        var (db, cleanup) = await CreateFreshDatabaseAsync($"PatientManagement_WalkInVisitNum_{Guid.NewGuid():N}");
        try
        {
            var alice = new Patient { Name = "Alice", Gender = "Female" };
            var bob = new Patient { Name = "Bob", Gender = "Male" };
            db.Patients.AddRange(alice, bob);
            await db.SaveChangesAsync();

            var t0 = DateTimeOffset.Now;
            var aliceVisit1 = await new WalkInService(db, new FixedTimeProvider(t0))
                .CreateWalkInVisitAsync(MakeRequest(alice.PatientId));
            var bobVisit1 = await new WalkInService(db, new FixedTimeProvider(t0.AddMinutes(1)))
                .CreateWalkInVisitAsync(MakeRequest(bob.PatientId));
            var aliceVisit2 = await new WalkInService(db, new FixedTimeProvider(t0.AddMinutes(2)))
                .CreateWalkInVisitAsync(MakeRequest(alice.PatientId));

            Assert.Equal(1, aliceVisit1.VisitNumber);
            Assert.Equal(1, bobVisit1.VisitNumber); // Bob's first visit is also #1, not #2
            Assert.Equal(2, aliceVisit2.VisitNumber);
        }
        finally
        {
            await cleanup();
        }
    }

    [Fact]
    public async Task Rejects_a_second_appointment_at_an_already_occupied_instant()
    {
        var (db, cleanup) = await CreateFreshDatabaseAsync($"PatientManagement_WalkInConflict_{Guid.NewGuid():N}");
        try
        {
            var alice = new Patient { Name = "Alice", Gender = "Female" };
            var bob = new Patient { Name = "Bob", Gender = "Male" };
            db.Patients.AddRange(alice, bob);
            await db.SaveChangesAsync();

            var sameInstant = new FixedTimeProvider(DateTimeOffset.Now);
            await new WalkInService(db, sameInstant).CreateWalkInVisitAsync(MakeRequest(alice.PatientId));

            await Assert.ThrowsAsync<AppointmentSlotConflictException>(() =>
                new WalkInService(db, sameInstant).CreateWalkInVisitAsync(MakeRequest(bob.PatientId)));

            Assert.Single(db.Appointments); // the rejected attempt inserted nothing
        }
        finally
        {
            await cleanup();
        }
    }

    [Fact]
    public async Task Weight_round_trips_at_three_decimal_places_without_rounding()
    {
        var (db, cleanup) = await CreateFreshDatabaseAsync($"PatientManagement_WalkInWeight_{Guid.NewGuid():N}");
        try
        {
            var patient = new Patient { Name = "Alice", Gender = "Female" };
            db.Patients.Add(patient);
            await db.SaveChangesAsync();

            var service = new WalkInService(db, new FixedTimeProvider(DateTimeOffset.Now));
            var request = MakeRequest(patient.PatientId) with { Weight = 52.850m };
            var visit = await service.CreateWalkInVisitAsync(request);

            var reloaded = await db.Visits.AsNoTracking().SingleAsync(v => v.Id == visit.Id);
            Assert.Equal(52.850m, reloaded.Weight);
        }
        finally
        {
            await cleanup();
        }
    }
}
