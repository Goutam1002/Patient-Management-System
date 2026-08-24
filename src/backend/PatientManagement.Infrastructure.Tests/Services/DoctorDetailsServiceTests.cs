using Microsoft.EntityFrameworkCore;
using PatientManagement.Application.DTOs;
using PatientManagement.Infrastructure.Data;
using PatientManagement.Infrastructure.Services;
using Xunit;

namespace PatientManagement.Infrastructure.Tests.Services;

public class DoctorDetailsServiceTests
{
    private static AppDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task Get_before_any_save_returns_sensible_defaults_without_creating_a_row()
    {
        await using var db = CreateContext(nameof(Get_before_any_save_returns_sensible_defaults_without_creating_a_row));
        var service = new DoctorDetailsService(db);

        var result = await service.GetAsync();

        Assert.Equal(string.Empty, result.ClinicName);
        Assert.Equal(string.Empty, result.DoctorName);
        Assert.Null(result.Logo);
        Assert.Null(result.Signature);
        Assert.Empty(await db.DoctorDetails.ToListAsync());
    }

    [Fact]
    public async Task Update_creates_the_row_when_none_exists_and_it_can_be_read_back()
    {
        await using var db = CreateContext(nameof(Update_creates_the_row_when_none_exists_and_it_can_be_read_back));
        var service = new DoctorDetailsService(db);

        await service.UpdateAsync(new UpdateDoctorDetailsRequest { ClinicName = "Sunrise Clinic", DoctorName = "Dr. Rao" });
        var result = await service.GetAsync();

        Assert.Equal("Sunrise Clinic", result.ClinicName);
        Assert.Equal("Dr. Rao", result.DoctorName);
        Assert.Single(await db.DoctorDetails.ToListAsync());
    }

    [Fact]
    public async Task Repeated_updates_never_create_more_than_one_row()
    {
        await using var db = CreateContext(nameof(Repeated_updates_never_create_more_than_one_row));
        var service = new DoctorDetailsService(db);

        await service.UpdateAsync(new UpdateDoctorDetailsRequest { ClinicName = "First", DoctorName = "Dr. A" });
        await service.UpdateAsync(new UpdateDoctorDetailsRequest { ClinicName = "Second", DoctorName = "Dr. B" });
        await service.UpdateAsync(new UpdateDoctorDetailsRequest { ClinicName = "Third", DoctorName = "Dr. C" });

        var rows = await db.DoctorDetails.ToListAsync();
        Assert.Single(rows);
        Assert.Equal("Third", rows[0].ClinicName);
    }

    [Fact]
    public async Task Logo_and_signature_round_trip_as_bytes()
    {
        await using var db = CreateContext(nameof(Logo_and_signature_round_trip_as_bytes));
        var service = new DoctorDetailsService(db);
        var logoBytes = new byte[] { 1, 2, 3, 4 };
        var signatureBytes = new byte[] { 200, 201 };

        var result = await service.UpdateAsync(new UpdateDoctorDetailsRequest
        {
            ClinicName = "Clinic",
            DoctorName = "Doctor",
            Logo = Convert.ToBase64String(logoBytes),
            Signature = Convert.ToBase64String(signatureBytes),
        });

        Assert.Equal(logoBytes, Convert.FromBase64String(result.Logo!));
        Assert.Equal(signatureBytes, Convert.FromBase64String(result.Signature!));

        var entity = await db.DoctorDetails.SingleAsync();
        Assert.Equal(logoBytes, entity.Logo);
        Assert.Equal(signatureBytes, entity.Signature);
    }

    [Fact]
    public async Task Omitting_logo_on_update_leaves_the_previously_saved_logo_unchanged()
    {
        await using var db = CreateContext(nameof(Omitting_logo_on_update_leaves_the_previously_saved_logo_unchanged));
        var service = new DoctorDetailsService(db);
        var logoBytes = new byte[] { 9, 9, 9 };
        await service.UpdateAsync(new UpdateDoctorDetailsRequest { ClinicName = "Clinic", DoctorName = "Doctor", Logo = Convert.ToBase64String(logoBytes) });

        var result = await service.UpdateAsync(new UpdateDoctorDetailsRequest { ClinicName = "Clinic Renamed", DoctorName = "Doctor" });

        Assert.Equal("Clinic Renamed", result.ClinicName);
        Assert.Equal(logoBytes, Convert.FromBase64String(result.Logo!));
    }
}
