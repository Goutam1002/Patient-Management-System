using Microsoft.EntityFrameworkCore;
using PatientManagement.Application.DTOs;
using PatientManagement.Application.Services;
using PatientManagement.Domain.Models;
using PatientManagement.Infrastructure.Data;

namespace PatientManagement.Infrastructure.Services;

public class PatientService(AppDbContext db) : IPatientService
{
    public async Task<PatientDto> CreateAsync(CreatePatientRequest request)
    {
        var entity = new Patient
        {
            Name = request.Name,
            Age = request.Age,
            DateOfBirth = request.DateOfBirth,
            Gender = request.Gender,
            Phone = request.Phone,
            Allergies = request.Allergies,
            CurrentMedications = request.CurrentMedications,
            ChronicConditions = request.ChronicConditions,
            EmergencyContactName = request.EmergencyContactName,
            EmergencyContactPhone = request.EmergencyContactPhone,
        };

        db.Patients.Add(entity);
        await db.SaveChangesAsync();
        return ToDto(entity);
    }

    public async Task<PatientDto?> GetAsync(int patientId)
    {
        var entity = await db.Patients.FindAsync(patientId);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<PatientDto?> UpdateAsync(int patientId, UpdatePatientRequest request)
    {
        var entity = await db.Patients.FindAsync(patientId);
        if (entity is null)
        {
            return null;
        }

        entity.Name = request.Name;
        entity.Age = request.Age;
        entity.DateOfBirth = request.DateOfBirth;
        entity.Gender = request.Gender;
        entity.Phone = request.Phone;
        entity.Allergies = request.Allergies;
        entity.CurrentMedications = request.CurrentMedications;
        entity.ChronicConditions = request.ChronicConditions;
        entity.EmergencyContactName = request.EmergencyContactName;
        entity.EmergencyContactPhone = request.EmergencyContactPhone;

        await db.SaveChangesAsync();
        return ToDto(entity);
    }

    public async Task<IReadOnlyList<PatientDto>> SearchAsync(string? name, string? phone)
    {
        var hasName = !string.IsNullOrWhiteSpace(name);
        var hasPhone = !string.IsNullOrWhiteSpace(phone);
        if (!hasName && !hasPhone)
        {
            return [];
        }

        var query = db.Patients.AsQueryable();

        // Case-insensitive Contains (not StartsWith) on both fields, per
        // implementation-brd.md's fixed Search spec -- a substring match
        // anywhere in the field, combined with AND when both are supplied
        // since they're two independent query parameters, not one search box.
        if (hasName)
        {
            var lowerName = name!.ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(lowerName));
        }

        if (hasPhone)
        {
            var lowerPhone = phone!.ToLower();
            query = query.Where(p => p.Phone != null && p.Phone.ToLower().Contains(lowerPhone));
        }

        var entities = await query.OrderBy(p => p.Name).ToListAsync();
        return entities.Select(ToDto).ToList();
    }

    private static PatientDto ToDto(Patient entity) => new()
    {
        PatientId = entity.PatientId,
        Name = entity.Name,
        Age = entity.Age,
        DateOfBirth = entity.DateOfBirth,
        Gender = entity.Gender,
        Phone = entity.Phone,
        Allergies = entity.Allergies,
        CurrentMedications = entity.CurrentMedications,
        ChronicConditions = entity.ChronicConditions,
        EmergencyContactName = entity.EmergencyContactName,
        EmergencyContactPhone = entity.EmergencyContactPhone,
    };
}
