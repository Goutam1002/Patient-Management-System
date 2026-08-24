using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
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
            });

            services.AddControllers()
                .ConfigureApplicationPartManager(manager =>
                    manager.ApplicationParts.Add(
                        new Microsoft.AspNetCore.Mvc.ApplicationParts.AssemblyPart(typeof(TestOnlyProtectedController).Assembly)));
        });
    }
}
