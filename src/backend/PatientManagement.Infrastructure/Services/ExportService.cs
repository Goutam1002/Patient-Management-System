using System.IO.Compression;
using System.Reflection;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PatientManagement.Application.DTOs;
using PatientManagement.Application.Services;
using PatientManagement.Domain.Models;
using PatientManagement.Infrastructure.Data;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace PatientManagement.Infrastructure.Services;

public class ExportService(AppDbContext db, TimeProvider timeProvider) : IExportService
{
    // Every public Patient property, in declaration order -- derived
    // dynamically so patients.csv automatically picks up any field the
    // entity gains later, per the fixed Export spec ("don't hardcode a
    // column list shorter than the actual entity").
    private static readonly PropertyInfo[] PatientProperties =
        typeof(Patient).GetProperties(BindingFlags.Public | BindingFlags.Instance);

    // Declared here (not only in Program.cs) so PDF generation also works
    // under the test host, which never runs Program.cs's startup code.
    static ExportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<byte[]> ExportCsvAsync(ExportCsvRequest request, string username)
    {
        if (!request.Confirmed)
        {
            throw new ExportNotConfirmedException("CSV export must be explicitly confirmed.");
        }

        var hasPatientIds = request.Scope.PatientIds is { Count: > 0 };
        var hasDateRange = request.Scope.DateFrom is not null && request.Scope.DateTo is not null;
        if (!hasPatientIds && !hasDateRange)
        {
            // The hard gate: no scope supplied is rejected, never treated as
            // "export everything" -- there is no unbounded/full-database path.
            throw new ExportScopeInvalidException(
                "Export scope must be either a non-empty list of patient IDs or a bounded date range (both dateFrom and dateTo).");
        }

        var query = db.Visits.AsNoTracking().Include(v => v.Appointment).Include(v => v.Patient).AsQueryable();
        query = hasPatientIds
            ? query.Where(v => request.Scope.PatientIds!.Contains(v.PatientId))
            : ApplyDateRange(query, request.Scope.DateFrom!.Value, request.Scope.DateTo!.Value);

        var rows = await LoadRowsAsync(query.OrderBy(v => v.PatientId).ThenBy(v => v.VisitNumber));

        var patientsCsv = BuildPatientsCsv(rows);
        var visitsCsv = BuildVisitsCsv(rows);
        var zipBytes = BuildZip(("patients.csv", patientsCsv), ("visits.csv", visitsCsv));

        var scopeDetail = hasPatientIds
            ? string.Join(",", request.Scope.PatientIds!)
            : $"{request.Scope.DateFrom:yyyy-MM-dd}..{request.Scope.DateTo:yyyy-MM-dd}";
        await LogExportAsync(
            ExportFormat.Csv,
            hasPatientIds ? ExportScopeType.SelectedPatients : ExportScopeType.DateRange,
            scopeDetail,
            username);

        return zipBytes;
    }

    public async Task<byte[]?> ExportPdfAsync(ExportPdfRequest request, string username)
    {
        if (!request.Confirmed)
        {
            throw new ExportNotConfirmedException("PDF export must be explicitly confirmed.");
        }

        var patient = await db.Patients.AsNoTracking().SingleOrDefaultAsync(p => p.PatientId == request.PatientId);
        if (patient is null)
        {
            return null;
        }

        var query = db.Visits.AsNoTracking().Include(v => v.Appointment).Include(v => v.Patient)
            .Where(v => v.PatientId == request.PatientId);
        if (request.DateFrom is not null)
        {
            var fromInclusive = request.DateFrom.Value.ToDateTime(TimeOnly.MinValue);
            query = query.Where(v => v.Appointment!.ScheduledTime >= fromInclusive);
        }
        if (request.DateTo is not null)
        {
            var toExclusive = request.DateTo.Value.ToDateTime(TimeOnly.MinValue).AddDays(1);
            query = query.Where(v => v.Appointment!.ScheduledTime < toExclusive);
        }

        var rows = await LoadRowsAsync(query.OrderBy(v => v.Appointment!.ScheduledTime));
        var pdfBytes = BuildPdf(patient, rows);

        var scopeDetail = request.DateFrom is not null && request.DateTo is not null
            ? $"PatientId={request.PatientId};{request.DateFrom:yyyy-MM-dd}..{request.DateTo:yyyy-MM-dd}"
            : $"PatientId={request.PatientId}";
        await LogExportAsync(ExportFormat.Pdf, ExportScopeType.SelectedPatients, scopeDetail, username);

        return pdfBytes;
    }

    public async Task<IReadOnlyList<ExportAuditLogDto>> GetAuditLogAsync()
    {
        var logs = await db.ExportAuditLogs
            .AsNoTracking()
            .Include(l => l.User)
            .OrderByDescending(l => l.PerformedAt)
            .ToListAsync();

        return logs
            .Select(l => new ExportAuditLogDto
            {
                Id = l.Id,
                PerformedAt = l.PerformedAt,
                Format = l.Format.ToString(),
                ScopeType = l.ScopeType.ToString(),
                ScopeDetail = l.ScopeDetail,
                Username = l.User?.Username ?? string.Empty,
            })
            .ToList();
    }

    private static IQueryable<Visit> ApplyDateRange(IQueryable<Visit> query, DateOnly from, DateOnly to)
    {
        // Same half-open-range technique PatientHistoryService/RecentPatientsService
        // use, so a whole-day inclusive range translates identically on SQL Server
        // and the in-memory provider.
        var fromInclusive = from.ToDateTime(TimeOnly.MinValue);
        var toExclusive = to.ToDateTime(TimeOnly.MinValue).AddDays(1);
        return query.Where(v => v.Appointment!.ScheduledTime >= fromInclusive && v.Appointment!.ScheduledTime < toExclusive);
    }

    private async Task<List<VisitExportRow>> LoadRowsAsync(IQueryable<Visit> orderedQuery)
    {
        var visits = await orderedQuery.ToListAsync();
        var visitIds = visits.Select(v => v.Id).ToList();

        var prescriptions = await db.Prescriptions
            .AsNoTracking()
            .Include(p => p.Items)
            .Where(p => visitIds.Contains(p.VisitId))
            .ToListAsync();
        var byVisit = prescriptions.GroupBy(p => p.VisitId).ToDictionary(g => g.Key, g => g.ToList());

        return visits
            .Select(v => new VisitExportRow(
                v.Patient!,
                v.Appointment!.ScheduledTime,
                v.Diagnosis,
                byVisit.TryGetValue(v.Id, out var list) ? list : []))
            .ToList();
    }

    private async Task LogExportAsync(ExportFormat format, ExportScopeType scopeType, string scopeDetail, string username)
    {
        var userId = await db.Users.Where(u => u.Username == username).Select(u => u.Id).SingleAsync();
        db.ExportAuditLogs.Add(new ExportAuditLog
        {
            PerformedAt = timeProvider.GetLocalNow().LocalDateTime,
            Format = format,
            ScopeType = scopeType,
            ScopeDetail = scopeDetail,
            UserId = userId,
        });
        await db.SaveChangesAsync();
    }

    private static string BuildPatientsCsv(List<VisitExportRow> rows)
    {
        var sb = new StringBuilder();
        var headers = PatientProperties.Select(p => p.Name).Concat(["VisitDate", "Diagnosis", "Prescriptions"]);
        sb.AppendLine(string.Join(',', headers));

        foreach (var row in rows)
        {
            var patientValues = PatientProperties.Select(p => CsvField(p.GetValue(row.Patient)));
            var extraValues = new[] { CsvField(row.VisitDate), CsvField(row.Diagnosis), CsvField(FormatPrescriptions(row.Prescriptions)) };
            sb.AppendLine(string.Join(',', patientValues.Concat(extraValues)));
        }

        return sb.ToString();
    }

    private static string BuildVisitsCsv(List<VisitExportRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("PatientId,Name,DOB,Phone,VisitDate,Diagnosis,Prescriptions");

        foreach (var row in rows)
        {
            var values = new[]
            {
                CsvField(row.Patient.PatientId),
                CsvField(row.Patient.Name),
                CsvField(row.Patient.DateOfBirth),
                CsvField(row.Patient.Phone),
                CsvField(row.VisitDate),
                CsvField(row.Diagnosis),
                CsvField(FormatPrescriptions(row.Prescriptions)),
            };
            sb.AppendLine(string.Join(',', values));
        }

        return sb.ToString();
    }

    private static string FormatPrescriptions(List<Prescription> prescriptions) =>
        string.Join("; ", prescriptions.SelectMany(p => p.Items).Select(i => $"{i.DrugName} ({i.Dosage}, {i.Frequency})"));

    private static string CsvField(object? value)
    {
        var text = value switch
        {
            null => string.Empty,
            DateOnly d => d.ToString("yyyy-MM-dd"),
            DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss"),
            _ => value.ToString() ?? string.Empty,
        };

        return text.IndexOfAny([',', '"', '\n', '\r']) >= 0
            ? $"\"{text.Replace("\"", "\"\"")}\""
            : text;
    }

    private static byte[] BuildZip(params (string Name, string Content)[] files)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in files)
            {
                var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
                using var entryStream = entry.Open();
                using var writer = new StreamWriter(entryStream, Encoding.UTF8);
                writer.Write(content);
            }
        }

        return stream.ToArray();
    }

    private static byte[] BuildPdf(Patient patient, List<VisitExportRow> rows)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(30);
                page.Header().Column(header =>
                {
                    header.Item().Text($"Patient Summary: {patient.Name}").FontSize(18).Bold();
                });

                page.Content().Column(column =>
                {
                    column.Item().PaddingBottom(10).Column(demographics =>
                    {
                        demographics.Item().Text($"Patient ID: {patient.PatientId}");
                        demographics.Item().Text(
                            $"DOB: {(patient.DateOfBirth?.ToString("yyyy-MM-dd") ?? "-")}    " +
                            $"Age: {(patient.Age?.ToString() ?? "-")}    Gender: {patient.Gender}");
                        demographics.Item().Text($"Phone: {patient.Phone ?? "-"}");
                        if (!string.IsNullOrWhiteSpace(patient.Allergies))
                            demographics.Item().Text($"Allergies: {patient.Allergies}");
                        if (!string.IsNullOrWhiteSpace(patient.CurrentMedications))
                            demographics.Item().Text($"Current Medications: {patient.CurrentMedications}");
                        if (!string.IsNullOrWhiteSpace(patient.ChronicConditions))
                            demographics.Item().Text($"Chronic Conditions: {patient.ChronicConditions}");
                        if (!string.IsNullOrWhiteSpace(patient.EmergencyContactName))
                            demographics.Item().Text($"Emergency Contact: {patient.EmergencyContactName} ({patient.EmergencyContactPhone})");
                    });

                    column.Item().Text("Visit History").FontSize(14).Bold();

                    if (rows.Count == 0)
                    {
                        column.Item().PaddingTop(4).Text("No visits in the selected range.");
                    }

                    foreach (var row in rows)
                    {
                        column.Item().PaddingTop(6).Text($"{row.VisitDate:yyyy-MM-dd} — Diagnosis: {row.Diagnosis ?? "-"}").Bold();
                        foreach (var item in row.Prescriptions.SelectMany(p => p.Items))
                        {
                            column.Item().PaddingLeft(10).Text($"- {item.DrugName} ({item.Dosage}, {item.Frequency})");
                        }
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }

    private record VisitExportRow(Patient Patient, DateTime VisitDate, string? Diagnosis, List<Prescription> Prescriptions);
}
