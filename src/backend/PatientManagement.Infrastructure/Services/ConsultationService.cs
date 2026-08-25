using Microsoft.EntityFrameworkCore;
using PatientManagement.Application.DTOs;
using PatientManagement.Application.Services;
using PatientManagement.Domain.Models;
using PatientManagement.Infrastructure.Data;

namespace PatientManagement.Infrastructure.Services;

public class ConsultationService(AppDbContext db) : IConsultationService
{
    public async Task<VisitDto?> StartConsultationAsync(int appointmentId, StartConsultationRequest request)
    {
        var appointment = await db.Appointments.FirstOrDefaultAsync(a => a.Id == appointmentId);
        if (appointment is null)
        {
            return null;
        }

        // An appointment produces at most one visit (unique index on
        // Visit.AppointmentId) -- check up front for a clean named exception
        // instead of letting the insert fail on the DB constraint.
        var alreadyHasVisit = await db.Visits.AnyAsync(v => v.AppointmentId == appointmentId);
        if (alreadyHasVisit)
        {
            throw new ConsultationAlreadyStartedException(appointmentId);
        }

        // Same per-patient visit-numbering query WalkInService uses, so the
        // two paths that create visits (walk-in, scheduled-consultation)
        // cannot drift out of sync with each other.
        var highestExistingVisitNumber = await db.Visits
            .Where(v => v.PatientId == appointment.PatientId)
            .Select(v => (int?)v.VisitNumber)
            .MaxAsync();
        var nextVisitNumber = (highestExistingVisitNumber ?? 0) + 1;

        var visit = new Visit
        {
            PatientId = appointment.PatientId,
            AppointmentId = appointment.Id,
            VisitNumber = nextVisitNumber,
            Temperature = request.Temperature!.Value,
            BpSystolic = request.BpSystolic!.Value,
            BpDiastolic = request.BpDiastolic!.Value,
            Pulse = request.Pulse!.Value,
            Weight = request.Weight!.Value,
            Complaints = request.Complaints,
            Diagnosis = request.Diagnosis,
        };
        db.Visits.Add(visit);

        // Completed is set only as a side effect of recording a visit, never
        // by hand -- same rule AppointmentService.UpdateStatusAsync enforces
        // for the manual-status-change path (see docs/implementation-progress.md
        // Step 12). The appointment update and the visit insert both go
        // through this one SaveChangesAsync call, so they commit atomically
        // without needing WalkInService's explicit transaction (that one
        // exists because it spans two separate SaveChangesAsync calls --
        // inserting the Appointment itself, then the Visit -- which this
        // path doesn't need since the Appointment already exists).
        appointment.Status = AppointmentStatus.Completed;

        await db.SaveChangesAsync();

        return ToDto(visit);
    }

    public async Task<VisitDto?> GetAsync(int visitId)
    {
        var visit = await db.Visits.AsNoTracking().FirstOrDefaultAsync(v => v.Id == visitId);
        return visit is null ? null : ToDto(visit);
    }

    public async Task<VisitDto?> UpdateAsync(int visitId, UpdateVisitRequest request)
    {
        var visit = await db.Visits.FirstOrDefaultAsync(v => v.Id == visitId);
        if (visit is null)
        {
            return null;
        }

        // Only these two fields are ever touched here -- UpdateVisitRequest
        // has no vitals properties, so there is nothing else to assign.
        visit.Complaints = request.Complaints;
        visit.Diagnosis = request.Diagnosis;

        await db.SaveChangesAsync();

        return ToDto(visit);
    }

    private static VisitDto ToDto(Visit visit) => new()
    {
        Id = visit.Id,
        PatientId = visit.PatientId,
        AppointmentId = visit.AppointmentId,
        VisitNumber = visit.VisitNumber,
        Temperature = visit.Temperature,
        BpSystolic = visit.BpSystolic,
        BpDiastolic = visit.BpDiastolic,
        Pulse = visit.Pulse,
        Weight = visit.Weight,
        Complaints = visit.Complaints,
        Diagnosis = visit.Diagnosis,
    };
}
