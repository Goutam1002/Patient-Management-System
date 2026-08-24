using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PatientManagement.Application.Services;
using PatientManagement.Domain.Models;

namespace PatientManagement.Infrastructure.Data;

/// <summary>
/// One-time seed for the single doctor account. Idempotent: only inserts
/// when Users is empty, so it's safe to call on every startup.
/// </summary>
public static class DoctorAccountSeeder
{
    public static async Task SeedAsync(AppDbContext db, IPasswordCrypto passwordCrypto, IConfiguration configuration)
    {
        if (await db.Users.AnyAsync())
        {
            return;
        }

        var username = configuration["Auth:SeedDoctorUsername"] ?? "doctor";
        var plaintextPassword = configuration["Auth:SeedDoctorPassword"] ?? "ChangeMe123!";

        db.Users.Add(new User
        {
            Username = username,
            Password = passwordCrypto.Encrypt(plaintextPassword),
        });

        await db.SaveChangesAsync();
    }
}
