using Microsoft.EntityFrameworkCore;
using PatientManagement.Application.DTOs;
using PatientManagement.Application.Services;
using PatientManagement.Domain.Models;
using PatientManagement.Infrastructure.Data;

namespace PatientManagement.Infrastructure.Services;

public class PatientHistoryService(AppDbContext db) : IPatientHistoryService
{
    public async Task<IReadOnlyList<VisitSummaryDto>> GetVisitsAsync(int patientId, DateOnly? from, DateOnly? to)
    {
        var query = db.Visits
            .AsNoTracking()
            .Include(v => v.Appointment)
            .Where(v => v.PatientId == patientId);

        // Half-open ranges (same technique AppointmentService.GetDailyAsync
        // uses) so both bounds translate identically on SQL Server and the
        // in-memory provider, while still reading as an inclusive whole-day
        // range from the caller's point of view.
        if (from is not null)
        {
            var fromInclusive = from.Value.ToDateTime(TimeOnly.MinValue);
            query = query.Where(v => v.Appointment!.ScheduledTime >= fromInclusive);
        }

        if (to is not null)
        {
            var toExclusive = to.Value.ToDateTime(TimeOnly.MinValue).AddDays(1);
            query = query.Where(v => v.Appointment!.ScheduledTime < toExclusive);
        }

        var visits = await query
            .OrderByDescending(v => v.Appointment!.ScheduledTime)
            .ToListAsync();

        return visits.Select(ToSummaryDto).ToList();
    }

    public async Task<VisitDetailDto?> GetVisitDetailAsync(int visitId)
    {
        var visit = await db.Visits
            .AsNoTracking()
            .Include(v => v.Appointment)
            .FirstOrDefaultAsync(v => v.Id == visitId);
        if (visit is null)
        {
            return null;
        }

        var prescriptions = await db.Prescriptions
            .AsNoTracking()
            .Include(p => p.Items)
            .Where(p => p.VisitId == visitId)
            .ToListAsync();

        return ToDetailDto(visit, prescriptions);
    }

    private static VisitSummaryDto ToSummaryDto(Visit visit) => new()
    {
        Id = visit.Id,
        PatientId = visit.PatientId,
        VisitNumber = visit.VisitNumber,
        VisitDate = visit.Appointment!.ScheduledTime,
        Diagnosis = visit.Diagnosis,
    };

    private static VisitDetailDto ToDetailDto(Visit visit, List<Prescription> prescriptions) => new()
    {
        Id = visit.Id,
        PatientId = visit.PatientId,
        AppointmentId = visit.AppointmentId,
        VisitNumber = visit.VisitNumber,
        VisitDate = visit.Appointment!.ScheduledTime,
        Temperature = visit.Temperature,
        BpSystolic = visit.BpSystolic,
        BpDiastolic = visit.BpDiastolic,
        Pulse = visit.Pulse,
        Weight = visit.Weight,
        Complaints = visit.Complaints,
        Diagnosis = visit.Diagnosis,
        Prescriptions = prescriptions.Select(ToPrescriptionDto).ToList(),
    };

    // Same mapping PrescriptionService.ToDto performs -- duplicated rather
    // than shared, consistent with every other service in this codebase
    // owning its own private ToDto (see PatientService, AppointmentService).
    private static PrescriptionDto ToPrescriptionDto(Prescription entity) => new()
    {
        Id = entity.Id,
        VisitId = entity.VisitId,
        CreatedAt = entity.CreatedAt,
        ClinicName = entity.ClinicName,
        DoctorName = entity.DoctorName,
        Qualifications = entity.Qualifications,
        RegistrationNumber = entity.RegistrationNumber,
        Logo = entity.Logo is null ? null : Convert.ToBase64String(entity.Logo),
        Signature = entity.Signature is null ? null : Convert.ToBase64String(entity.Signature),
        Items = entity.Items
            .Select(i => new PrescriptionItemDto
            {
                Id = i.Id,
                DrugName = i.DrugName,
                Dosage = i.Dosage,
                Frequency = i.Frequency,
                Duration = i.Duration,
                Instructions = i.Instructions,
            })
            .ToList(),
    };
}
