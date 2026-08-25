using Microsoft.EntityFrameworkCore;
using PatientManagement.Application.DTOs;
using PatientManagement.Domain.Models;
using PatientManagement.Infrastructure.Data;
using PatientManagement.Infrastructure.Services;
using Xunit;

namespace PatientManagement.Infrastructure.Tests.Services;

/// <summary>Fixed-instant clock so CreatedAt lands deterministically.</summary>
file sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

public class PrescriptionServiceTests
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

    private static async Task<Visit> SeedVisitAsync(AppDbContext db)
    {
        var patient = new Patient { Name = "Alice", Gender = "Female" };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        var appointment = new Appointment
        {
            PatientId = patient.PatientId,
            ScheduledTime = new DateTime(2026, 4, 1, 9, 0, 0),
            DurationMinutes = 15,
            Status = AppointmentStatus.Completed,
        };
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();

        var visit = new Visit
        {
            PatientId = patient.PatientId,
            AppointmentId = appointment.Id,
            VisitNumber = 1,
            Temperature = 37.0m,
            BpSystolic = 120,
            BpDiastolic = 80,
            Pulse = 72,
            Weight = 52.850m,
            Diagnosis = "URI",
        };
        db.Visits.Add(visit);
        await db.SaveChangesAsync();
        return visit;
    }

    private static CreatePrescriptionRequest MakeRequest(params (string DrugName, string? Dosage)[] items) => new()
    {
        Items = items
            .Select(i => new CreatePrescriptionItemRequest { DrugName = i.DrugName, Dosage = i.Dosage })
            .ToList(),
    };

    [Fact]
    public async Task Create_snapshots_todays_doctor_details_and_persists_every_line_item()
    {
        var (db, cleanup) = await CreateFreshDatabaseAsync($"PatientManagement_RxCreate_{Guid.NewGuid():N}");
        try
        {
            db.DoctorDetails.Add(new DoctorDetails
            {
                ClinicName = "Sunrise Clinic",
                DoctorName = "Dr. Rao",
                RegistrationNumber = "REG-42",
            });
            await db.SaveChangesAsync();
            var visit = await SeedVisitAsync(db);

            var service = new PrescriptionService(db, new FixedTimeProvider(DateTimeOffset.Now));
            var result = await service.CreatePrescriptionAsync(
                visit.Id,
                MakeRequest(("Paracetamol", "500mg"), ("Cetirizine", "10mg")));

            Assert.NotNull(result);
            Assert.Equal(visit.Id, result!.VisitId);
            Assert.Equal("Sunrise Clinic", result.ClinicName);
            Assert.Equal("Dr. Rao", result.DoctorName);
            Assert.Equal("REG-42", result.RegistrationNumber);
            Assert.Equal(2, result.Items.Count);
            Assert.Contains(result.Items, i => i.DrugName == "Paracetamol" && i.Dosage == "500mg");
        }
        finally
        {
            await cleanup();
        }
    }

    [Fact]
    public async Task Create_for_an_unknown_visit_returns_null_and_writes_nothing()
    {
        var (db, cleanup) = await CreateFreshDatabaseAsync($"PatientManagement_RxUnknownVisit_{Guid.NewGuid():N}");
        try
        {
            var service = new PrescriptionService(db, new FixedTimeProvider(DateTimeOffset.Now));
            var result = await service.CreatePrescriptionAsync(4242, MakeRequest(("Paracetamol", null)));

            Assert.Null(result);
            Assert.Empty(db.Prescriptions);
        }
        finally
        {
            await cleanup();
        }
    }

    [Fact]
    public async Task Create_without_a_saved_doctor_details_row_falls_back_to_empty_strings_instead_of_failing()
    {
        var (db, cleanup) = await CreateFreshDatabaseAsync($"PatientManagement_RxNoDoctorDetails_{Guid.NewGuid():N}");
        try
        {
            var visit = await SeedVisitAsync(db); // no DoctorDetails row seeded

            var service = new PrescriptionService(db, new FixedTimeProvider(DateTimeOffset.Now));
            var result = await service.CreatePrescriptionAsync(visit.Id, MakeRequest(("Paracetamol", null)));

            Assert.NotNull(result);
            Assert.Equal(string.Empty, result!.ClinicName);
            Assert.Equal(string.Empty, result.DoctorName);
        }
        finally
        {
            await cleanup();
        }
    }

    [Fact]
    public async Task A_correction_creates_a_new_prescription_row_rather_than_mutating_the_first()
    {
        var (db, cleanup) = await CreateFreshDatabaseAsync($"PatientManagement_RxCorrection_{Guid.NewGuid():N}");
        try
        {
            var visit = await SeedVisitAsync(db);
            var service = new PrescriptionService(db, new FixedTimeProvider(DateTimeOffset.Now));

            var first = await service.CreatePrescriptionAsync(visit.Id, MakeRequest(("Paracetamol", "500mg")));
            var second = await service.CreatePrescriptionAsync(visit.Id, MakeRequest(("Amoxicillin", "250mg")));

            Assert.NotEqual(first!.Id, second!.Id);
            Assert.Equal(2, await db.Prescriptions.CountAsync());

            // The first prescription's own content is untouched by the second call.
            var reloadedFirst = await service.GetAsync(first.Id);
            Assert.Equal("Paracetamol", Assert.Single(reloadedFirst!.Items).DrugName);
        }
        finally
        {
            await cleanup();
        }
    }

    [Fact]
    public async Task Get_for_an_unknown_prescription_returns_null()
    {
        var (db, cleanup) = await CreateFreshDatabaseAsync($"PatientManagement_RxGetUnknown_{Guid.NewGuid():N}");
        try
        {
            var service = new PrescriptionService(db, new FixedTimeProvider(DateTimeOffset.Now));
            var result = await service.GetAsync(4242);

            Assert.Null(result);
        }
        finally
        {
            await cleanup();
        }
    }
}
