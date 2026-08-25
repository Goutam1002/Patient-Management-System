using Microsoft.EntityFrameworkCore;
using PatientManagement.Domain.Models;
using PatientManagement.Infrastructure.Data;
using PatientManagement.Infrastructure.Services;
using Xunit;

namespace PatientManagement.Infrastructure.Tests.Services;

public class DrugSuggestionServiceTests
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
            ScheduledTime = DateTime.Now,
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
        };
        db.Visits.Add(visit);
        await db.SaveChangesAsync();
        return visit;
    }

    private static async Task SeedPrescriptionAsync(AppDbContext db, Visit visit, params string[] drugNames)
    {
        var prescription = Prescription.CreateFromDoctorDetails(
            visit.Id, new DoctorDetails { ClinicName = "Clinic", DoctorName = "Dr. A" }, DateTime.Now);
        foreach (var drugName in drugNames)
        {
            prescription.Items.Add(new PrescriptionItem { DrugName = drugName });
        }
        db.Prescriptions.Add(prescription);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Matches_a_substring_occurring_anywhere_in_the_drug_name_not_only_a_prefix()
    {
        var (db, cleanup) = await CreateFreshDatabaseAsync($"PatientManagement_DrugSuggest_{Guid.NewGuid():N}");
        try
        {
            var visit = await SeedVisitAsync(db);
            await SeedPrescriptionAsync(db, visit, "Amoxicillin", "Paracetamol");

            var service = new DrugSuggestionService(db);
            var results = await service.GetSuggestionsAsync("oxic"); // mid-string of Amoxicillin

            Assert.Single(results);
            Assert.Equal("Amoxicillin", results[0]);
        }
        finally
        {
            await cleanup();
        }
    }

    [Fact]
    public async Task Matching_is_case_insensitive()
    {
        var (db, cleanup) = await CreateFreshDatabaseAsync($"PatientManagement_DrugSuggestCase_{Guid.NewGuid():N}");
        try
        {
            var visit = await SeedVisitAsync(db);
            await SeedPrescriptionAsync(db, visit, "Amoxicillin");

            var service = new DrugSuggestionService(db);
            var results = await service.GetSuggestionsAsync("AMOX");

            Assert.Single(results);
        }
        finally
        {
            await cleanup();
        }
    }

    [Fact]
    public async Task Results_are_distinct_even_when_the_same_drug_was_prescribed_multiple_times()
    {
        var (db, cleanup) = await CreateFreshDatabaseAsync($"PatientManagement_DrugSuggestDistinct_{Guid.NewGuid():N}");
        try
        {
            var visit1 = await SeedVisitAsync(db);
            await SeedPrescriptionAsync(db, visit1, "Paracetamol");
            var visit2 = await SeedVisitAsync(db);
            await SeedPrescriptionAsync(db, visit2, "Paracetamol");

            var service = new DrugSuggestionService(db);
            var results = await service.GetSuggestionsAsync(null);

            Assert.Single(results);
        }
        finally
        {
            await cleanup();
        }
    }

    [Fact]
    public async Task A_blank_term_returns_every_distinct_drug_name_on_record()
    {
        var (db, cleanup) = await CreateFreshDatabaseAsync($"PatientManagement_DrugSuggestBlank_{Guid.NewGuid():N}");
        try
        {
            var visit = await SeedVisitAsync(db);
            await SeedPrescriptionAsync(db, visit, "Amoxicillin", "Paracetamol");

            var service = new DrugSuggestionService(db);
            var results = await service.GetSuggestionsAsync("");

            Assert.Equal(2, results.Count);
        }
        finally
        {
            await cleanup();
        }
    }
}
