using Microsoft.EntityFrameworkCore;
using PatientManagement.Api.Data;
using PatientManagement.Api.DTOs;
using PatientManagement.Api.Models;

namespace PatientManagement.Api.Services;

public class WalkInService(AppDbContext db, TimeProvider timeProvider) : IWalkInService
{
    public async Task<Visit> CreateWalkInVisitAsync(WalkInVisitRequest request)
    {
        var scheduledTime = timeProvider.GetLocalNow().LocalDateTime;

        var slotTaken = await db.Appointments.AnyAsync(a => a.ScheduledTime == scheduledTime);
        if (slotTaken)
        {
            throw new AppointmentSlotConflictException(scheduledTime);
        }

        var highestExistingVisitNumber = await db.Visits
            .Where(v => v.PatientId == request.PatientId)
            .Select(v => (int?)v.VisitNumber)
            .MaxAsync();
        var nextVisitNumber = (highestExistingVisitNumber ?? 0) + 1;

        await using var transaction = await db.Database.BeginTransactionAsync();

        var appointment = new Appointment
        {
            PatientId = request.PatientId,
            ScheduledTime = scheduledTime,
            DurationMinutes = request.DurationMinutes,
            Status = AppointmentStatus.Completed,
        };
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();

        var visit = new Visit
        {
            PatientId = request.PatientId,
            AppointmentId = appointment.Id,
            VisitNumber = nextVisitNumber,
            Temperature = request.Temperature,
            BpSystolic = request.BpSystolic,
            BpDiastolic = request.BpDiastolic,
            Pulse = request.Pulse,
            Weight = request.Weight,
            Complaints = request.Complaints,
            Diagnosis = request.Diagnosis,
        };
        db.Visits.Add(visit);
        await db.SaveChangesAsync();

        await transaction.CommitAsync();

        return visit;
    }
}
