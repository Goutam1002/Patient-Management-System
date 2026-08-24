using Microsoft.EntityFrameworkCore;
using PatientManagement.Domain.Models;
using PatientManagement.Infrastructure.Data;
using Xunit;

namespace PatientManagement.Infrastructure.Tests.Data;

/// <summary>
/// Confirms PatientId starts at 0 (not EF/SQL Server's default seed of 1)
/// and that Phone can legitimately repeat across patients. Requires a real
/// SQL Server (LocalDB) connection -- EF Core's InMemory provider doesn't
/// honor IDENTITY(0,1) seed/increment annotations.
/// </summary>
public class PatientIdentitySeedTests
{
    private static async Task<(AppDbContext Db, Func<Task> Cleanup)> CreateFreshDatabaseAsync(string dbName)
    {
        var connectionString = $"Server=(localdb)\\MSSQLLocalDB;Database={dbName};Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        var db = new AppDbContext(options);
        await db.Database.MigrateAsync();

        return (db, async () =>
        {
            await db.Database.EnsureDeletedAsync();
            await db.DisposeAsync();
        });
    }

    [Fact]
    public async Task First_patient_gets_id_zero_and_second_gets_id_one()
    {
        var (db, cleanup) = await CreateFreshDatabaseAsync($"PatientManagement_PatientId_{Guid.NewGuid():N}");
        try
        {
            var first = new Patient { Name = "Alice", Gender = "Female" };
            var second = new Patient { Name = "Bob", Gender = "Male" };

            db.Patients.Add(first);
            await db.SaveChangesAsync();
            db.Patients.Add(second);
            await db.SaveChangesAsync();

            Assert.Equal(0, first.PatientId);
            Assert.Equal(1, second.PatientId);
        }
        finally
        {
            await cleanup();
        }
    }

    [Fact]
    public async Task Two_patients_may_share_the_same_phone_number()
    {
        var (db, cleanup) = await CreateFreshDatabaseAsync($"PatientManagement_PatientPhone_{Guid.NewGuid():N}");
        try
        {
            db.Patients.Add(new Patient { Name = "Alice", Gender = "Female", Phone = "9876543210" });
            db.Patients.Add(new Patient { Name = "Bob", Gender = "Male", Phone = "9876543210" });

            // Must not throw a uniqueness-constraint violation.
            await db.SaveChangesAsync();

            Assert.Equal(2, await db.Patients.CountAsync(p => p.Phone == "9876543210"));
        }
        finally
        {
            await cleanup();
        }
    }

    [Fact]
    public async Task Age_and_DateOfBirth_persist_independently()
    {
        var (db, cleanup) = await CreateFreshDatabaseAsync($"PatientManagement_PatientAgeDob_{Guid.NewGuid():N}");
        try
        {
            var dob = new DateOnly(1990, 5, 14);
            db.Patients.Add(new Patient { Name = "Alice", Gender = "Female", Age = 34, DateOfBirth = dob });
            await db.SaveChangesAsync();

            var reloaded = await db.Patients.AsNoTracking().SingleAsync();

            // Neither value is derived from the other -- both are exactly
            // what was stored, even though 34 isn't necessarily today's age
            // computed from 1990-05-14.
            Assert.Equal(34, reloaded.Age);
            Assert.Equal(dob, reloaded.DateOfBirth);
        }
        finally
        {
            await cleanup();
        }
    }
}
