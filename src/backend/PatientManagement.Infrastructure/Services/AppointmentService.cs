using Microsoft.EntityFrameworkCore;
using PatientManagement.Application.DTOs;
using PatientManagement.Application.Services;
using PatientManagement.Domain.Models;
using PatientManagement.Infrastructure.Data;

namespace PatientManagement.Infrastructure.Services;

public class AppointmentService(AppDbContext db) : IAppointmentService
{
    public async Task<AppointmentDto?> CreateAsync(CreateAppointmentRequest request)
    {
        var patient = await db.Patients.FindAsync(request.PatientId);
        if (patient is null)
        {
            return null;
        }

        // DurationMinutes is validated as [Required] on the request, so a
        // caller that reached here supplied one explicitly -- nothing is
        // defaulted on this side either.
        var scheduledTime = request.ScheduledTime!.Value;
        await AppointmentSlotGuard.EnsureSlotIsFreeAsync(db, scheduledTime);

        var appointment = new Appointment
        {
            PatientId = request.PatientId,
            ScheduledTime = scheduledTime,
            DurationMinutes = request.DurationMinutes!.Value,
            Status = AppointmentStatus.Scheduled,
        };

        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();

        return ToDto(appointment, patient.Name, visitId: null);
    }

    public async Task<IReadOnlyList<AppointmentDto>> GetDailyAsync(DateOnly date)
    {
        // Half-open range rather than a .Date comparison so the query
        // translates identically on SQL Server and the in-memory provider.
        var dayStart = date.ToDateTime(TimeOnly.MinValue);
        var dayEnd = dayStart.AddDays(1);

        var appointments = await db.Appointments
            .AsNoTracking()
            .Include(a => a.Patient)
            .Where(a => a.ScheduledTime >= dayStart && a.ScheduledTime < dayEnd)
            .OrderBy(a => a.ScheduledTime)
            .ToListAsync();

        if (appointments.Count == 0)
        {
            return [];
        }

        var appointmentIds = appointments.Select(a => a.Id).ToList();
        var visitIdsByAppointment = await db.Visits
            .AsNoTracking()
            .Where(v => appointmentIds.Contains(v.AppointmentId))
            .ToDictionaryAsync(v => v.AppointmentId, v => v.Id);

        return appointments
            .Select(a => ToDto(
                a,
                a.Patient?.Name ?? string.Empty,
                visitIdsByAppointment.TryGetValue(a.Id, out var visitId) ? visitId : null))
            .ToList();
    }

    public async Task<AppointmentDto?> UpdateStatusAsync(int appointmentId, AppointmentStatus status)
    {
        // FLAGGED ASSUMPTION (see docs/implementation-progress.md Step 12):
        // Completed is not a status the doctor sets by hand here -- it is only
        // ever set as a side effect of creating a visit (walk-in today, the
        // consultation workflow in Module 5). The rule was recorded as a
        // recommendation in docs/plan-brd-review.md but never locked into
        // implementation-brd.md, so this is an explicitly flagged decision and
        // is cheap to reverse: delete this guard and its two tests.
        if (status == AppointmentStatus.Completed)
        {
            throw new AppointmentStatusTransitionException(
                status,
                "An appointment cannot be marked Completed directly. Completion happens only as a side effect of recording the patient's visit.");
        }

        var appointment = await db.Appointments
            .Include(a => a.Patient)
            .FirstOrDefaultAsync(a => a.Id == appointmentId);
        if (appointment is null)
        {
            return null;
        }

        appointment.Status = status;
        await db.SaveChangesAsync();

        var visitId = await db.Visits
            .Where(v => v.AppointmentId == appointment.Id)
            .Select(v => (int?)v.Id)
            .FirstOrDefaultAsync();

        return ToDto(appointment, appointment.Patient?.Name ?? string.Empty, visitId);
    }

    private static AppointmentDto ToDto(Appointment appointment, string patientName, int? visitId) => new()
    {
        Id = appointment.Id,
        PatientId = appointment.PatientId,
        PatientName = patientName,
        ScheduledTime = appointment.ScheduledTime,
        DurationMinutes = appointment.DurationMinutes,
        Status = appointment.Status,
        VisitId = visitId,
    };
}
