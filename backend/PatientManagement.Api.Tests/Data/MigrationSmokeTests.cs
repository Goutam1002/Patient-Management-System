using Microsoft.EntityFrameworkCore;
using PatientManagement.Api.Data;
using Xunit;

namespace PatientManagement.Api.Tests.Data;

/// <summary>
/// Applies every migration to a fresh, uniquely-named database on the local
/// SQL Server instance and confirms it succeeds. Requires (localdb)\MSSQLLocalDB
/// -- the same instance used for local development, never a shared/production one.
/// </summary>
public class MigrationSmokeTests
{
    [Fact]
    public async Task All_migrations_apply_cleanly_to_a_fresh_database()
    {
        var dbName = $"PatientManagement_MigrationSmoke_{Guid.NewGuid():N}";
        var connectionString = $"Server=(localdb)\\MSSQLLocalDB;Database={dbName};Trusted_Connection=True;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        await using var db = new AppDbContext(options);
        try
        {
            await db.Database.MigrateAsync();

            var pendingMigrations = await db.Database.GetPendingMigrationsAsync();
            Assert.Empty(pendingMigrations);

            var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
            Assert.Contains(appliedMigrations, m => m.EndsWith("_InitialCreate"));
            Assert.Contains(appliedMigrations, m => m.EndsWith("_AddUsers"));
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }
}
