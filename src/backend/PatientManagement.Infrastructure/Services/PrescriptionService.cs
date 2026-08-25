using Microsoft.EntityFrameworkCore;
using PatientManagement.Application.DTOs;
using PatientManagement.Application.Services;
using PatientManagement.Domain.Models;
using PatientManagement.Infrastructure.Data;

namespace PatientManagement.Infrastructure.Services;

public class PrescriptionService(AppDbContext db, TimeProvider timeProvider) : IPrescriptionService
{
    public async Task<PrescriptionDto?> CreatePrescriptionAsync(int visitId, CreatePrescriptionRequest request)
    {
        var visitExists = await db.Visits.AnyAsync(v => v.Id == visitId);
        if (!visitExists)
        {
            return null;
        }

        // Snapshot today's DoctorDetails -- the only sanctioned construction
        // path (Prescription.CreateFromDoctorDetails). If DoctorDetails has
        // never been saved, fall back to the same empty-string defaults
        // DoctorDetailsService.GetAsync() already serves before any row
        // exists, rather than blocking prescription creation on clinic setup
        // (implementation-time decision, not called out in the module's own
        // checklist -- cheap to tighten into a hard block later if wanted).
        var doctorDetails = await db.DoctorDetails.AsNoTracking().SingleOrDefaultAsync()
            ?? new DoctorDetails { ClinicName = string.Empty, DoctorName = string.Empty };

        var prescription = Prescription.CreateFromDoctorDetails(
            visitId, doctorDetails, timeProvider.GetLocalNow().LocalDateTime);

        foreach (var item in request.Items)
        {
            prescription.Items.Add(new PrescriptionItem
            {
                DrugName = item.DrugName,
                Dosage = item.Dosage,
                Frequency = item.Frequency,
                Duration = item.Duration,
                Instructions = item.Instructions,
            });
        }

        db.Prescriptions.Add(prescription);
        await db.SaveChangesAsync();

        return ToDto(prescription);
    }

    public async Task<PrescriptionDto?> GetAsync(int prescriptionId)
    {
        var prescription = await db.Prescriptions
            .Include(p => p.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == prescriptionId);

        return prescription is null ? null : ToDto(prescription);
    }

    private static PrescriptionDto ToDto(Prescription entity) => new()
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
