using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PatientManagement.Application.Services;
using PatientManagement.Domain.Models;
using PatientManagement.Infrastructure.Data;
using PatientManagement.Infrastructure.Services;
using Xunit;

namespace PatientManagement.Infrastructure.Tests.Data;

public class DoctorAccountSeederTests
{
    private static AppDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new AppDbContext(options);
    }

    private static IPasswordCrypto CreateCrypto()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:EncryptionKey"] = Convert.ToBase64String(new byte[32]),
            })
            .Build();
        return new AesPasswordCrypto(config);
    }

    [Fact]
    public async Task Seeds_exactly_one_user_with_encrypted_password_when_table_is_empty()
    {
        await using var db = CreateContext(nameof(Seeds_exactly_one_user_with_encrypted_password_when_table_is_empty));
        var crypto = CreateCrypto();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:SeedDoctorUsername"] = "doctor",
                ["Auth:SeedDoctorPassword"] = "ChangeMe123!",
            })
            .Build();

        await DoctorAccountSeeder.SeedAsync(db, crypto, config);

        var user = Assert.Single(db.Users);
        Assert.Equal("doctor", user.Username);
        Assert.NotEqual("ChangeMe123!", user.Password);
        Assert.Equal("ChangeMe123!", crypto.Decrypt(user.Password));
    }

    [Fact]
    public async Task Does_not_seed_again_when_a_user_already_exists()
    {
        await using var db = CreateContext(nameof(Does_not_seed_again_when_a_user_already_exists));
        var crypto = CreateCrypto();
        var config = new ConfigurationBuilder().Build();

        db.Users.Add(new User { Username = "existing", Password = "already-encrypted" });
        await db.SaveChangesAsync();

        await DoctorAccountSeeder.SeedAsync(db, crypto, config);

        var user = Assert.Single(db.Users);
        Assert.Equal("existing", user.Username);
    }
}
