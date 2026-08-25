using Microsoft.EntityFrameworkCore;
using PatientManagement.Domain.Models;
using PatientManagement.Infrastructure.Data;
using PatientManagement.Infrastructure.Services;
using Xunit;

namespace PatientManagement.Infrastructure.Tests.Services;

public class RecentPatientsServiceTests
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

    private static async Task<Patient> AddPatientAsync(AppDbContext db, string name)
    {
        var patient = new Patient { Name = name, Gender = "Female" };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();
        return patient;
    }

    private static async Task AddVisitAsync(AppDbContext db, Patient patient, DateTime scheduledTime)
    {
        var appointment = new Appointment
        {
            PatientId = patient.PatientId,
            ScheduledTime = scheduledTime,
            DurationMinutes = 15,
            Status = AppointmentStatus.Completed,
        };
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();

        db.Visits.Add(new Visit
        {
            PatientId = patient.PatientId,
            AppointmentId = appointment.Id,
            VisitNumber = 1,
            Temperature = 37,
            BpSystolic = 120,
            BpDiastolic = 80,
            Pulse = 70,
            Weight = 50,
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetRecent_ranks_by_most_recent_visit_date_not_registration_order()
    {
        var (db, cleanup) = await CreateFreshDatabaseAsync($"PatientManagement_RecentOrder_{Guid.NewGuid():N}");
        try
        {
            // Registered first, but visited longer ago -- should sort behind
            // a patient registered later but visited more recently.
            var registeredFirst = await AddPatientAsync(db, "RegisteredFirst");
            var registeredSecond = await AddPatientAsync(db, "RegisteredSecond");

            await AddVisitAsync(db, registeredFirst, new DateTime(2026, 1, 1, 9, 0, 0));
            await AddVisitAsync(db, registeredSecond, new DateTime(2026, 6, 1, 9, 0, 0));

            var service = new RecentPatientsService(db);
            var result = await service.GetRecentAsync(10);

            Assert.Equal(2, result.Count);
            Assert.Equal("RegisteredSecond", result[0].Name); // most-recently-visited first
            Assert.Equal("RegisteredFirst", result[1].Name);
        }
        finally
        {
            await cleanup();
        }
    }

    [Fact]
    public async Task GetRecent_places_a_patient_with_no_visits_yet_last()
    {
        var (db, cleanup) = await CreateFreshDatabaseAsync($"PatientManagement_RecentNoVisit_{Guid.NewGuid():N}");
        try
        {
            var noVisits = await AddPatientAsync(db, "NoVisits");
            var withVisit = await AddPatientAsync(db, "WithVisit");
            await AddVisitAsync(db, withVisit, new DateTime(2026, 1, 1, 9, 0, 0));

            var service = new RecentPatientsService(db);
            var result = await service.GetRecentAsync(10);

            Assert.Equal(2, result.Count);
            Assert.Equal("WithVisit", result[0].Name);
            Assert.Equal("NoVisits", result[1].Name);
            Assert.NotNull(result[0].LastVisitDate);
            Assert.Null(result[1].LastVisitDate);
        }
        finally
        {
            await cleanup();
        }
    }

    [Fact]
    public async Task GetRecent_uses_the_visit_with_the_latest_date_when_a_patient_has_several()
    {
        var (db, cleanup) = await CreateFreshDatabaseAsync($"PatientManagement_RecentMulti_{Guid.NewGuid():N}");
        try
        {
            var patient = await AddPatientAsync(db, "Repeat");
            await AddVisitAsync(db, patient, new DateTime(2026, 1, 1, 9, 0, 0));
            await AddVisitAsync(db, patient, new DateTime(2026, 5, 1, 9, 0, 0));

            var service = new RecentPatientsService(db);
            var result = await service.GetRecentAsync(10);

            Assert.Single(result);
            Assert.Equal(new DateTime(2026, 5, 1, 9, 0, 0), result[0].LastVisitDate);
        }
        finally
        {
            await cleanup();
        }
    }

    [Fact]
    public async Task GetRecent_respects_the_requested_count()
    {
        var (db, cleanup) = await CreateFreshDatabaseAsync($"PatientManagement_RecentCount_{Guid.NewGuid():N}");
        try
        {
            for (var i = 0; i < 3; i++)
            {
                var patient = await AddPatientAsync(db, $"Patient{i}");
                await AddVisitAsync(db, patient, new DateTime(2026, 1, 1 + i, 9, 0, 0));
            }

            var service = new RecentPatientsService(db);
            var result = await service.GetRecentAsync(2);

            Assert.Equal(2, result.Count);
            Assert.Equal("Patient2", result[0].Name);
            Assert.Equal("Patient1", result[1].Name);
        }
        finally
        {
            await cleanup();
        }
    }
}
