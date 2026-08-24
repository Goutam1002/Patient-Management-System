using Microsoft.EntityFrameworkCore;
using PatientManagement.Application.DTOs;
using PatientManagement.Application.Services;
using PatientManagement.Domain.Models;
using PatientManagement.Infrastructure.Data;

namespace PatientManagement.Infrastructure.Services;

public class DoctorDetailsService(AppDbContext db) : IDoctorDetailsService
{
    public async Task<DoctorDetailsDto> GetAsync()
    {
        var entity = await db.DoctorDetails.SingleOrDefaultAsync();
        return entity is null ? DefaultDto() : ToDto(entity);
    }

    public async Task<DoctorDetailsDto> UpdateAsync(UpdateDoctorDetailsRequest request)
    {
        // Effectively a singleton -- get the one existing row if present,
        // otherwise create it. Never Add a second row.
        var entity = await db.DoctorDetails.SingleOrDefaultAsync();
        if (entity is null)
        {
            entity = new DoctorDetails { ClinicName = request.ClinicName, DoctorName = request.DoctorName };
            db.DoctorDetails.Add(entity);
        }

        entity.ClinicName = request.ClinicName;
        entity.DoctorName = request.DoctorName;
        entity.Qualifications = request.Qualifications;
        entity.RegistrationNumber = request.RegistrationNumber;

        if (request.Logo is not null)
        {
            entity.Logo = Convert.FromBase64String(request.Logo);
        }

        if (request.Signature is not null)
        {
            entity.Signature = Convert.FromBase64String(request.Signature);
        }

        await db.SaveChangesAsync();
        return ToDto(entity);
    }

    private static DoctorDetailsDto DefaultDto() => new()
    {
        Id = 0,
        ClinicName = string.Empty,
        DoctorName = string.Empty,
    };

    private static DoctorDetailsDto ToDto(DoctorDetails entity) => new()
    {
        Id = entity.Id,
        ClinicName = entity.ClinicName,
        DoctorName = entity.DoctorName,
        Qualifications = entity.Qualifications,
        RegistrationNumber = entity.RegistrationNumber,
        Logo = entity.Logo is null ? null : Convert.ToBase64String(entity.Logo),
        Signature = entity.Signature is null ? null : Convert.ToBase64String(entity.Signature),
    };
}
