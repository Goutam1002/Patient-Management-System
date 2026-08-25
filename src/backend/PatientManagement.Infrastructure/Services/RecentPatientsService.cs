using Microsoft.EntityFrameworkCore;
using PatientManagement.Application.DTOs;
using PatientManagement.Application.Services;
using PatientManagement.Infrastructure.Data;

namespace PatientManagement.Infrastructure.Services;

public class RecentPatientsService(AppDbContext db) : IRecentPatientsService
{
    public async Task<IReadOnlyList<RecentPatientDto>> GetRecentAsync(int count)
    {
        // Aggregate each patient's most-recent visit date (via Appointment.ScheduledTime,
        // same VisitDate resolution PatientHistoryService/Step 15 established) first,
        // then rank in memory -- a local clinic's patient count is small enough that a
        // second in-memory pass (same technique PatientService.SearchAsync uses) is
        // simpler than forcing a single translatable LEFT JOIN + aggregate query.
        var lastVisitDates = await db.Visits
            .AsNoTracking()
            .Include(v => v.Appointment)
            .GroupBy(v => v.PatientId)
            .Select(g => new { PatientId = g.Key, LastVisitDate = g.Max(v => v.Appointment!.ScheduledTime) })
            .ToDictionaryAsync(x => x.PatientId, x => x.LastVisitDate);

        var patients = await db.Patients.AsNoTracking().ToListAsync();

        return patients
            .Select(p => new RecentPatientDto
            {
                PatientId = p.PatientId,
                Name = p.Name,
                Phone = p.Phone,
                LastVisitDate = lastVisitDates.TryGetValue(p.PatientId, out var date) ? date : null,
            })
            // Patients with a visit sort by most-recent first; a patient with no
            // visit yet has nothing to sort by and is placed last, not excluded
            // (implementation-time choice, documented in implementation-progress.md).
            .OrderByDescending(p => p.LastVisitDate.HasValue)
            .ThenByDescending(p => p.LastVisitDate)
            .Take(count)
            .ToList();
    }
}
