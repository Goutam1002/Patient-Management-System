using Microsoft.EntityFrameworkCore;
using PatientManagement.Application.DTOs;
using PatientManagement.Domain.Models;
using PatientManagement.Infrastructure.Data;
using PatientManagement.Infrastructure.Services;
using Xunit;

namespace PatientManagement.Infrastructure.Tests.Services;

public class PatientHistoryServiceTests
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

    private static async Task<(Patient Patient, Appointment Appointment, Visit Visit)> AddVisitAsync(
        AppDbContext db, DateTime scheduledTime, string diagnosis)
    {
        var patient = new Patient { Name = "Alice", Gender = "Female" };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        var appointment = new Appointment
        {
            PatientId = patient.PatientId,
            ScheduledTime = scheduledTime,
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
            Complaints = "Cough",
            Diagnosis = diagnosis,
        };
        db.Visits.Add(visit);
        await db.SaveChangesAsync();

        return (patient, appointment, visit);
    }

    [Fact]
    public async Task GetVisits_returns_visits_newest_first_by_visit_date()
    {
        var (db, cleanup) = await CreateFreshDatabaseAsync($"PatientManagement_HistOrder_{Guid.NewGuid():N}");
        try
        {
            var patient = new Patient { Name = "Alice", Gender = "Female" };
            db.Patients.Add(patient);
            await db.SaveChangesAsync();

            var earlier = new Appointment { PatientId = patient.PatientId, ScheduledTime = new DateTime(2026, 1, 5, 9, 0, 0), DurationMinutes = 15, Status = AppointmentStatus.Completed };
            var later = new Appointment { PatientId = patient.PatientId, ScheduledTime = new DateTime(2026, 3, 10, 9, 0, 0), DurationMinutes = 15, Status = AppointmentStatus.Completed };
            db.Appointments.AddRange(earlier, later);
            await db.SaveChangesAsync();

            db.Visits.AddRange(
                new Visit { PatientId = patient.PatientId, AppointmentId = earlier.Id, VisitNumber = 1, Temperature = 37, BpSystolic = 120, BpDiastolic = 80, Pulse = 70, Weight = 50, Diagnosis = "First" },
                new Visit { PatientId = patient.PatientId, AppointmentId = later.Id, VisitNumber = 2, Temperature = 37, BpSystolic = 120, BpDiastolic = 80, Pulse = 70, Weight = 50, Diagnosis = "Second" });
            await db.SaveChangesAsync();

            var service = new PatientHistoryService(db);
            var result = await service.GetVisitsAsync(patient.PatientId, from: null, to: null);

            Assert.Equal(2, result.Count);
            Assert.Equal("Second", result[0].Diagnosis); // most recent visit date first
            Assert.Equal("First", result[1].Diagnosis);
            Assert.Equal(later.ScheduledTime, result[0].VisitDate);
        }
        finally
        {
            await cleanup();
        }
    }

    [Fact]
    public async Task GetVisits_date_range_is_inclusive_on_both_boundaries()
    {
        var (db, cleanup) = await CreateFreshDatabaseAsync($"PatientManagement_HistRange_{Guid.NewGuid():N}");
        try
        {
            var patient = new Patient { Name = "Alice", Gender = "Female" };
            db.Patients.Add(patient);
            await db.SaveChangesAsync();

            // One visit exactly on the from-boundary, one exactly on the
            // to-boundary, one outside on each side.
            var beforeRange = new Appointment { PatientId = patient.PatientId, ScheduledTime = new DateTime(2026, 1, 31, 23, 0, 0), DurationMinutes = 15, Status = AppointmentStatus.Completed };
            var onFromBoundary = new Appointment { PatientId = patient.PatientId, ScheduledTime = new DateTime(2026, 2, 1, 0, 0, 0), DurationMinutes = 15, Status = AppointmentStatus.Completed };
            var onToBoundary = new Appointment { PatientId = patient.PatientId, ScheduledTime = new DateTime(2026, 2, 28, 23, 59, 0), DurationMinutes = 15, Status = AppointmentStatus.Completed };
            var afterRange = new Appointment { PatientId = patient.PatientId, ScheduledTime = new DateTime(2026, 3, 1, 0, 0, 0), DurationMinutes = 15, Status = AppointmentStatus.Completed };
            db.Appointments.AddRange(beforeRange, onFromBoundary, onToBoundary, afterRange);
            await db.SaveChangesAsync();

            var appointments = new[] { beforeRange, onFromBoundary, onToBoundary, afterRange };
            var i = 0;
            foreach (var appt in appointments)
            {
                i++;
                db.Visits.Add(new Visit
                {
                    PatientId = patient.PatientId,
                    AppointmentId = appt.Id,
                    VisitNumber = i,
                    Temperature = 37,
                    BpSystolic = 120,
                    BpDiastolic = 80,
                    Pulse = 70,
                    Weight = 50,
                    Diagnosis = $"Visit{i}",
                });
            }
            await db.SaveChangesAsync();

            var service = new PatientHistoryService(db);
            var result = await service.GetVisitsAsync(
                patient.PatientId,
                from: new DateOnly(2026, 2, 1),
                to: new DateOnly(2026, 2, 28));

            Assert.Equal(2, result.Count);
            Assert.Contains(result, v => v.Diagnosis == "Visit2");
            Assert.Contains(result, v => v.Diagnosis == "Visit3");
            Assert.DoesNotContain(result, v => v.Diagnosis == "Visit1");
            Assert.DoesNotContain(result, v => v.Diagnosis == "Visit4");
        }
        finally
        {
            await cleanup();
        }
    }

    [Fact]
    public async Task GetVisits_for_a_patient_with_no_visits_returns_an_empty_list()
    {
        var (db, cleanup) = await CreateFreshDatabaseAsync($"PatientManagement_HistEmpty_{Guid.NewGuid():N}");
        try
        {
            var service = new PatientHistoryService(db);
            var result = await service.GetVisitsAsync(4242, from: null, to: null);

            Assert.Empty(result);
        }
        finally
        {
            await cleanup();
        }
    }

    [Fact]
    public async Task GetVisitDetail_returns_the_full_field_set_including_visit_date_and_prescriptions()
    {
        var (db, cleanup) = await CreateFreshDatabaseAsync($"PatientManagement_HistDetail_{Guid.NewGuid():N}");
        try
        {
            var (_, appointment, visit) = await AddVisitAsync(db, new DateTime(2026, 4, 1, 9, 0, 0), "URI");

            var prescription = Prescription.CreateFromDoctorDetails(
                visit.Id,
                new DoctorDetails { ClinicName = "Clinic", DoctorName = "Dr. Bob" },
                new DateTime(2026, 4, 1, 9, 15, 0));
            prescription.Items.Add(new PrescriptionItem { DrugName = "Paracetamol", Dosage = "500mg", Frequency = "BD", Duration = "5 days" });
            db.Prescriptions.Add(prescription);
            await db.SaveChangesAsync();

            var service = new PatientHistoryService(db);
            var result = await service.GetVisitDetailAsync(visit.Id);

            Assert.NotNull(result);
            Assert.Equal(visit.Id, result!.Id);
            Assert.Equal(appointment.Id, result.AppointmentId);
            Assert.Equal(appointment.ScheduledTime, result.VisitDate);
            Assert.Equal(37.0m, result.Temperature);
            Assert.Equal((short)120, result.BpSystolic);
            Assert.Equal((short)80, result.BpDiastolic);
            Assert.Equal(72, result.Pulse);
            Assert.Equal(52.850m, result.Weight);
            Assert.Equal("Cough", result.Complaints);
            Assert.Equal("URI", result.Diagnosis);
            Assert.Single(result.Prescriptions);
            Assert.Equal("Paracetamol", result.Prescriptions[0].Items[0].DrugName);
        }
        finally
        {
            await cleanup();
        }
    }

    [Fact]
    public async Task GetVisitDetail_for_an_unknown_visit_returns_null()
    {
        var (db, cleanup) = await CreateFreshDatabaseAsync($"PatientManagement_HistDetailUnknown_{Guid.NewGuid():N}");
        try
        {
            var service = new PatientHistoryService(db);
            var result = await service.GetVisitDetailAsync(4242);

            Assert.Null(result);
        }
        finally
        {
            await cleanup();
        }
    }
}
