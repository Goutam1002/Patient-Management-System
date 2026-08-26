using System.IO.Compression;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PatientManagement.Application.DTOs;
using PatientManagement.Application.Services;
using PatientManagement.Domain.Models;
using PatientManagement.Infrastructure.Data;
using PatientManagement.Infrastructure.Services;
using Xunit;

namespace PatientManagement.Infrastructure.Tests.Services;

file sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

public class ExportServiceTests
{
    private const string SeedUsername = "doctor";

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

    private static async Task<int> SeedUserAsync(AppDbContext db)
    {
        var user = new User { Username = SeedUsername, Password = "irrelevant-for-this-test" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private static async Task<(Patient Patient, Visit Visit)> SeedVisitAsync(
        AppDbContext db, string name, DateTime scheduledTime, string diagnosis, string drugName)
    {
        var patient = new Patient
        {
            Name = name,
            Gender = "Female",
            Phone = "5550100",
            DateOfBirth = new DateOnly(1990, 1, 1),
            Allergies = "Penicillin",
        };
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
            Diagnosis = diagnosis,
        };
        db.Visits.Add(visit);
        await db.SaveChangesAsync();

        var prescription = Prescription.CreateFromDoctorDetails(
            visit.Id, new DoctorDetails { ClinicName = "Clinic", DoctorName = "Dr. Bob" }, scheduledTime);
        prescription.Items.Add(new PrescriptionItem { DrugName = drugName, Dosage = "500mg", Frequency = "BD" });
        db.Prescriptions.Add(prescription);
        await db.SaveChangesAsync();

        return (patient, visit);
    }

    private static Dictionary<string, string> ReadZipEntries(byte[] zipBytes)
    {
        using var stream = new MemoryStream(zipBytes);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        var entries = new Dictionary<string, string>();
        foreach (var entry in zip.Entries)
        {
            using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
            entries[entry.Name] = reader.ReadToEnd();
        }
        return entries;
    }

    [Fact]
    public async Task ExportCsv_with_no_scope_is_rejected_and_writes_nothing()
    {
        // The hard gate: neither patientIds nor a date range means the request
        // is rejected outright -- there is no code path that falls through to
        // "export every patient."
        var (db, cleanup) = await CreateFreshDatabaseAsync($"PatientManagement_ExportNoScope_{Guid.NewGuid():N}");
        try
        {
            await SeedUserAsync(db);
            var service = new ExportService(db, new FixedTimeProvider(DateTimeOffset.Now));

            var request = new ExportCsvRequest { Scope = new ExportScopeRequest(), Confirmed = true };
            await Assert.ThrowsAsync<ExportScopeInvalidException>(() => service.ExportCsvAsync(request, SeedUsername));

            Assert.Empty(db.ExportAuditLogs);
        }
        finally
        {
            await cleanup();
        }
    }

    [Fact]
    public async Task ExportCsv_without_confirmation_is_rejected_and_writes_nothing()
    {
        var (db, cleanup) = await CreateFreshDatabaseAsync($"PatientManagement_ExportUnconfirmed_{Guid.NewGuid():N}");
        try
        {
            await SeedUserAsync(db);
            var (patient, _) = await SeedVisitAsync(db, "Alice", new DateTime(2026, 1, 1), "URI", "Paracetamol");
            var service = new ExportService(db, new FixedTimeProvider(DateTimeOffset.Now));

            var request = new ExportCsvRequest
            {
                Scope = new ExportScopeRequest { PatientIds = [patient.PatientId] },
                Confirmed = false,
            };
            await Assert.ThrowsAsync<ExportNotConfirmedException>(() => service.ExportCsvAsync(request, SeedUsername));

            Assert.Empty(db.ExportAuditLogs);
        }
        finally
        {
            await cleanup();
        }
    }

    [Fact]
    public async Task ExportCsv_patients_csv_has_one_row_per_visit_with_every_patient_field_plus_visit_columns()
    {
        var (db, cleanup) = await CreateFreshDatabaseAsync($"PatientManagement_ExportPatientsCsv_{Guid.NewGuid():N}");
        try
        {
            await SeedUserAsync(db);
            var (alice, aliceVisit1) = await SeedVisitAsync(db, "Alice", new DateTime(2026, 1, 1, 9, 0, 0), "URI", "Paracetamol");

            // A second visit for the same patient -- demographics must repeat, not dedupe.
            var secondAppointment = new Appointment
            {
                PatientId = alice.PatientId,
                ScheduledTime = new DateTime(2026, 2, 1, 9, 0, 0),
                DurationMinutes = 15,
                Status = AppointmentStatus.Completed,
            };
            db.Appointments.Add(secondAppointment);
            await db.SaveChangesAsync();
            db.Visits.Add(new Visit
            {
                PatientId = alice.PatientId,
                AppointmentId = secondAppointment.Id,
                VisitNumber = 2,
                Temperature = 37.5m,
                BpSystolic = 118,
                BpDiastolic = 76,
                Pulse = 68,
                Weight = 53.0m,
                Diagnosis = "Follow-up",
            });
            await db.SaveChangesAsync();

            var service = new ExportService(db, new FixedTimeProvider(DateTimeOffset.Now));
            var zipBytes = await service.ExportCsvAsync(
                new ExportCsvRequest { Scope = new ExportScopeRequest { PatientIds = [alice.PatientId] }, Confirmed = true },
                SeedUsername);

            var entries = ReadZipEntries(zipBytes);
            Assert.True(entries.ContainsKey("patients.csv"));
            var lines = entries["patients.csv"].Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

            var header = lines[0];
            // Every current Patient field must appear, derived dynamically -- not a hardcoded shorter list.
            Assert.Contains("PatientId", header);
            Assert.Contains("Allergies", header);
            Assert.Contains("EmergencyContactName", header);
            Assert.Contains("VisitDate", header);
            Assert.Contains("Diagnosis", header);
            Assert.Contains("Prescriptions", header);

            // One row per visit -- two visits for the same patient means two data rows.
            Assert.Equal(3, lines.Length); // header + 2 visit rows
            Assert.Contains(lines, l => l.Contains("URI") && l.Contains("Paracetamol"));
            Assert.Contains(lines, l => l.Contains("Follow-up"));
            Assert.Contains(lines, l => l.StartsWith($"{alice.PatientId},Alice,"));
        }
        finally
        {
            await cleanup();
        }
    }

    [Fact]
    public async Task ExportCsv_visits_csv_has_fixed_column_order_and_semicolon_encoded_prescriptions()
    {
        var (db, cleanup) = await CreateFreshDatabaseAsync($"PatientManagement_ExportVisitsCsv_{Guid.NewGuid():N}");
        try
        {
            await SeedUserAsync(db);
            var (patient, visit) = await SeedVisitAsync(db, "Alice", new DateTime(2026, 1, 1, 9, 0, 0), "URI", "Paracetamol");

            // A second prescription line item on the same visit, to prove semicolon-joining.
            var secondPrescription = Prescription.CreateFromDoctorDetails(
                visit.Id, new DoctorDetails { ClinicName = "Clinic", DoctorName = "Dr. Bob" }, new DateTime(2026, 1, 1, 9, 5, 0));
            secondPrescription.Items.Add(new PrescriptionItem { DrugName = "Cetirizine", Dosage = "10mg", Frequency = "OD" });
            db.Prescriptions.Add(secondPrescription);
            await db.SaveChangesAsync();

            var service = new ExportService(db, new FixedTimeProvider(DateTimeOffset.Now));
            var zipBytes = await service.ExportCsvAsync(
                new ExportCsvRequest { Scope = new ExportScopeRequest { PatientIds = [patient.PatientId] }, Confirmed = true },
                SeedUsername);

            var entries = ReadZipEntries(zipBytes);
            Assert.True(entries.ContainsKey("visits.csv"));
            var lines = entries["visits.csv"].Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

            Assert.Equal("PatientId,Name,DOB,Phone,VisitDate,Diagnosis,Prescriptions", lines[0]);
            Assert.Equal(2, lines.Length); // header + one visit row
            Assert.Contains("Paracetamol (500mg, BD); Cetirizine (10mg, OD)", lines[1]);
        }
        finally
        {
            await cleanup();
        }
    }

    [Fact]
    public async Task ExportCsv_by_date_range_excludes_visits_outside_the_range()
    {
        var (db, cleanup) = await CreateFreshDatabaseAsync($"PatientManagement_ExportDateRange_{Guid.NewGuid():N}");
        try
        {
            await SeedUserAsync(db);
            await SeedVisitAsync(db, "InRange", new DateTime(2026, 2, 15, 9, 0, 0), "InRange", "DrugA");
            await SeedVisitAsync(db, "OutOfRange", new DateTime(2026, 5, 1, 9, 0, 0), "OutOfRange", "DrugB");

            var service = new ExportService(db, new FixedTimeProvider(DateTimeOffset.Now));
            var zipBytes = await service.ExportCsvAsync(
                new ExportCsvRequest
                {
                    Scope = new ExportScopeRequest { DateFrom = new DateOnly(2026, 2, 1), DateTo = new DateOnly(2026, 2, 28) },
                    Confirmed = true,
                },
                SeedUsername);

            var entries = ReadZipEntries(zipBytes);
            var lines = entries["visits.csv"].Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

            Assert.Equal(2, lines.Length); // header + one in-range visit
            Assert.Contains("InRange", lines[1]);
        }
        finally
        {
            await cleanup();
        }
    }

    [Fact]
    public async Task A_completed_export_writes_an_audit_log_entry_with_who_scope_format_and_when()
    {
        var (db, cleanup) = await CreateFreshDatabaseAsync($"PatientManagement_ExportAudit_{Guid.NewGuid():N}");
        try
        {
            var userId = await SeedUserAsync(db);
            var (patient, _) = await SeedVisitAsync(db, "Alice", new DateTime(2026, 1, 1), "URI", "Paracetamol");
            var now = new DateTimeOffset(2026, 6, 1, 10, 30, 0, TimeSpan.Zero);
            var service = new ExportService(db, new FixedTimeProvider(now));

            await service.ExportCsvAsync(
                new ExportCsvRequest { Scope = new ExportScopeRequest { PatientIds = [patient.PatientId] }, Confirmed = true },
                SeedUsername);

            var log = await db.ExportAuditLogs.SingleAsync();
            Assert.Equal(userId, log.UserId);
            Assert.Equal(ExportFormat.Csv, log.Format);
            Assert.Equal(ExportScopeType.SelectedPatients, log.ScopeType);
            Assert.Contains(patient.PatientId.ToString(), log.ScopeDetail);
            Assert.Equal(now.LocalDateTime, log.PerformedAt);
        }
        finally
        {
            await cleanup();
        }
    }

    [Fact]
    public async Task ExportPdf_for_an_unknown_patient_returns_null_and_writes_nothing()
    {
        var (db, cleanup) = await CreateFreshDatabaseAsync($"PatientManagement_ExportPdfUnknown_{Guid.NewGuid():N}");
        try
        {
            await SeedUserAsync(db);
            var service = new ExportService(db, new FixedTimeProvider(DateTimeOffset.Now));

            var result = await service.ExportPdfAsync(
                new ExportPdfRequest { PatientId = 4242, Confirmed = true }, SeedUsername);

            Assert.Null(result);
            Assert.Empty(db.ExportAuditLogs);
        }
        finally
        {
            await cleanup();
        }
    }

    [Fact]
    public async Task ExportPdf_without_confirmation_is_rejected()
    {
        var (db, cleanup) = await CreateFreshDatabaseAsync($"PatientManagement_ExportPdfUnconfirmed_{Guid.NewGuid():N}");
        try
        {
            await SeedUserAsync(db);
            var (patient, _) = await SeedVisitAsync(db, "Alice", new DateTime(2026, 1, 1), "URI", "Paracetamol");
            var service = new ExportService(db, new FixedTimeProvider(DateTimeOffset.Now));

            var request = new ExportPdfRequest { PatientId = patient.PatientId, Confirmed = false };
            await Assert.ThrowsAsync<ExportNotConfirmedException>(() => service.ExportPdfAsync(request, SeedUsername));

            Assert.Empty(db.ExportAuditLogs);
        }
        finally
        {
            await cleanup();
        }
    }

    [Fact]
    public async Task ExportPdf_for_a_known_patient_produces_a_non_empty_pdf_and_logs_the_export()
    {
        var (db, cleanup) = await CreateFreshDatabaseAsync($"PatientManagement_ExportPdfOk_{Guid.NewGuid():N}");
        try
        {
            var userId = await SeedUserAsync(db);
            var (patient, _) = await SeedVisitAsync(db, "Alice", new DateTime(2026, 1, 1), "URI", "Paracetamol");
            var service = new ExportService(db, new FixedTimeProvider(DateTimeOffset.Now));

            var result = await service.ExportPdfAsync(
                new ExportPdfRequest { PatientId = patient.PatientId, Confirmed = true }, SeedUsername);

            Assert.NotNull(result);
            // A real PDF starts with the "%PDF-" magic header.
            Assert.Equal("%PDF-", Encoding.ASCII.GetString(result!, 0, 5));

            var log = await db.ExportAuditLogs.SingleAsync();
            Assert.Equal(userId, log.UserId);
            Assert.Equal(ExportFormat.Pdf, log.Format);
        }
        finally
        {
            await cleanup();
        }
    }

    [Fact]
    public async Task GetAuditLog_returns_entries_newest_first_with_username_resolved()
    {
        var (db, cleanup) = await CreateFreshDatabaseAsync($"PatientManagement_ExportAuditLog_{Guid.NewGuid():N}");
        try
        {
            await SeedUserAsync(db);
            var (patient, _) = await SeedVisitAsync(db, "Alice", new DateTime(2026, 1, 1), "URI", "Paracetamol");

            var service = new ExportService(db, new FixedTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));
            await service.ExportCsvAsync(
                new ExportCsvRequest { Scope = new ExportScopeRequest { PatientIds = [patient.PatientId] }, Confirmed = true },
                SeedUsername);

            var laterService = new ExportService(db, new FixedTimeProvider(new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero)));
            await laterService.ExportPdfAsync(
                new ExportPdfRequest { PatientId = patient.PatientId, Confirmed = true }, SeedUsername);

            var log = await service.GetAuditLogAsync();

            Assert.Equal(2, log.Count);
            Assert.Equal("Pdf", log[0].Format); // most recent first
            Assert.Equal("Csv", log[1].Format);
            Assert.All(log, l => Assert.Equal(SeedUsername, l.Username));
        }
        finally
        {
            await cleanup();
        }
    }
}
