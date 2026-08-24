using Microsoft.EntityFrameworkCore;
using PatientManagement.Api.Data;
using PatientManagement.Api.Models;
using Xunit;

namespace PatientManagement.Api.Tests.Data;

public class PrescriptionSnapshotTests
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

    [Fact]
    public async Task Editing_DoctorDetails_after_creation_does_not_change_an_existing_prescriptions_snapshot()
    {
        var (db, cleanup) = await CreateFreshDatabaseAsync($"PatientManagement_RxSnapshot_{Guid.NewGuid():N}");
        try
        {
            var doctorDetails = new DoctorDetails
            {
                ClinicName = "Original Clinic",
                DoctorName = "Dr. Original",
                RegistrationNumber = "REG-001",
            };
            db.DoctorDetails.Add(doctorDetails);

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

            var prescription = Prescription.CreateFromDoctorDetails(visit.Id, doctorDetails, DateTime.Now);
            db.Prescriptions.Add(prescription);
            await db.SaveChangesAsync();

            // Now edit DoctorDetails -- a later change to the clinic's live
            // record must not retroactively alter the already-printed prescription.
            doctorDetails.ClinicName = "Renamed Clinic";
            doctorDetails.DoctorName = "Dr. New";
            doctorDetails.RegistrationNumber = "REG-002";
            await db.SaveChangesAsync();

            var reloadedPrescription = await db.Prescriptions.AsNoTracking().SingleAsync(p => p.Id == prescription.Id);

            Assert.Equal("Original Clinic", reloadedPrescription.ClinicName);
            Assert.Equal("Dr. Original", reloadedPrescription.DoctorName);
            Assert.Equal("REG-001", reloadedPrescription.RegistrationNumber);
        }
        finally
        {
            await cleanup();
        }
    }

    [Fact]
    public async Task Prescription_items_are_free_text_with_no_drug_dictionary_constraint()
    {
        var (db, cleanup) = await CreateFreshDatabaseAsync($"PatientManagement_RxItems_{Guid.NewGuid():N}");
        try
        {
            var doctorDetails = new DoctorDetails { ClinicName = "Clinic", DoctorName = "Dr. A" };
            var patient = new Patient { Name = "Alice", Gender = "Female" };
            db.DoctorDetails.Add(doctorDetails);
            db.Patients.Add(patient);
            await db.SaveChangesAsync();

            var appointment = new Appointment { PatientId = patient.PatientId, ScheduledTime = DateTime.Now, DurationMinutes = 15, Status = AppointmentStatus.Completed };
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

            var prescription = Prescription.CreateFromDoctorDetails(visit.Id, doctorDetails, DateTime.Now);
            prescription.Items.Add(new PrescriptionItem
            {
                DrugName = "A brand-new drug not in any dictionary",
                Dosage = "500mg",
                Frequency = "twice daily",
                Duration = "5 days",
            });
            db.Prescriptions.Add(prescription);
            await db.SaveChangesAsync();

            var reloaded = await db.Prescriptions.Include(p => p.Items).AsNoTracking().SingleAsync(p => p.Id == prescription.Id);
            Assert.Single(reloaded.Items);
            Assert.Equal("A brand-new drug not in any dictionary", reloaded.Items.Single().DrugName);
        }
        finally
        {
            await cleanup();
        }
    }
}
