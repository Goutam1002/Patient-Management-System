using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PatientManagement.Infrastructure.Data;
using Xunit;

namespace PatientManagement.Infrastructure.Tests.Data;

/// <summary>
/// Confirms the Users table's actual, migrated schema -- not just the EF
/// model -- exposes exactly Id/Username/Password, per implementation-brd.md's
/// build gate ("no extra auth-related columns: no PasswordHash, Salt,
/// MfaSecret, etc."). Requires (localdb)\MSSQLLocalDB.
/// </summary>
public class UsersSchemaTests
{
    [Fact]
    public async Task Users_table_has_exactly_Id_Username_Password_columns()
    {
        var dbName = $"PatientManagement_UsersSchema_{Guid.NewGuid():N}";
        var connectionString = $"Server=(localdb)\\MSSQLLocalDB;Database={dbName};Trusted_Connection=True;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        await using var db = new AppDbContext(options);
        try
        {
            await db.Database.MigrateAsync();

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Users'";

            var columns = new List<string>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns.Add(reader.GetString(0));
            }

            Assert.Equal(
                new[] { "Id", "Password", "Username" },
                columns.OrderBy(c => c, StringComparer.Ordinal));
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }
}
