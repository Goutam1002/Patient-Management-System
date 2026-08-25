using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PatientManagement.Infrastructure.Data;

namespace PatientManagement.Api.Tests;

public class AuthApiFactory : WebApplicationFactory<Program>
{
    public const string SeedUsername = "test-doctor";
    public const string SeedPassword = "TestPassword123!";

    private readonly string _databaseName = Guid.NewGuid().ToString();

    /// <summary>
    /// Replaces the app's TimeProvider.System registration when set, so a test
    /// can pin "now" and force a walk-in to land on a slot it chose. Left null
    /// by every test that doesn't care about the clock.
    /// </summary>
    public TimeProvider? Clock { get; init; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Test-only key/credentials, unrelated to appsettings.Development.json's real values.
                ["Auth:EncryptionKey"] = Convert.ToBase64String(new byte[32]),
                ["Auth:SeedDoctorUsername"] = SeedUsername,
                ["Auth:SeedDoctorPassword"] = SeedPassword,
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();

            // Program.cs registers the SqlServer provider for AppDbContext.
            // Simply swapping the options to UseInMemoryDatabase would leave
            // both the SqlServer and InMemory provider services registered
            // in the same container, which EF Core rejects at runtime -- so
            // the InMemory provider gets its own isolated internal service
            // provider instead of sharing the app's container.
            var inMemoryProvider = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName);
                options.UseInternalServiceProvider(inMemoryProvider);
                // WalkInService wraps its Appointment+Visit insert in a real
                // transaction. The in-memory provider has no transactions and
                // throws on BeginTransaction unless this warning is downgraded.
                // Atomicity itself is still covered for real, against LocalDB,
                // by Infrastructure.Tests' WalkInServiceTests -- these API
                // tests are about the HTTP contract, not the transaction.
                options.ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
            });

            if (Clock is not null)
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton(Clock);
            }

            services.AddControllers()
                .ConfigureApplicationPartManager(manager =>
                    manager.ApplicationParts.Add(
                        new Microsoft.AspNetCore.Mvc.ApplicationParts.AssemblyPart(typeof(TestOnlyProtectedController).Assembly)));
        });
    }
}
