using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PatientManagement.Application.DTOs;
using PatientManagement.Application.Services;
using PatientManagement.Domain.Models;
using PatientManagement.Infrastructure.Data;
using PatientManagement.Infrastructure.Services;
using Xunit;

namespace PatientManagement.Infrastructure.Tests.Services;

public class LoginServiceTests
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

    private static async Task<AppDbContext> SeededContext(string dbName, IPasswordCrypto crypto, string username, string password)
    {
        var db = CreateContext(dbName);
        db.Users.Add(new User { Username = username, Password = crypto.Encrypt(password) });
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task Correct_credentials_return_a_result_with_a_session_token()
    {
        var crypto = CreateCrypto();
        await using var db = await SeededContext(nameof(Correct_credentials_return_a_result_with_a_session_token), crypto, "doctor", "ChangeMe123!");
        var sessionTokenStore = new InMemorySessionTokenStore();
        var loginService = new LoginService(db, crypto, sessionTokenStore);

        var result = await loginService.LoginAsync(new LoginRequest { Username = "doctor", Password = "ChangeMe123!" });

        Assert.NotNull(result);
        Assert.Equal("doctor", result!.Username);
        Assert.True(sessionTokenStore.TryGetUsername(result.SessionToken, out var resolvedUsername));
        Assert.Equal("doctor", resolvedUsername);
    }

    [Fact]
    public async Task Wrong_username_returns_null()
    {
        var crypto = CreateCrypto();
        await using var db = await SeededContext(nameof(Wrong_username_returns_null), crypto, "doctor", "ChangeMe123!");
        var loginService = new LoginService(db, crypto, new InMemorySessionTokenStore());

        var result = await loginService.LoginAsync(new LoginRequest { Username = "not-the-doctor", Password = "ChangeMe123!" });

        Assert.Null(result);
    }

    [Fact]
    public async Task Wrong_password_returns_null()
    {
        var crypto = CreateCrypto();
        await using var db = await SeededContext(nameof(Wrong_password_returns_null), crypto, "doctor", "ChangeMe123!");
        var loginService = new LoginService(db, crypto, new InMemorySessionTokenStore());

        var result = await loginService.LoginAsync(new LoginRequest { Username = "doctor", Password = "WrongPassword!" });

        Assert.Null(result);
    }
}
