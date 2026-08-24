using Microsoft.EntityFrameworkCore;
using PatientManagement.Application.DTOs;
using PatientManagement.Infrastructure.Data;
using PatientManagement.Infrastructure.Services;
using Xunit;

namespace PatientManagement.Infrastructure.Tests.Services;

public class PatientServiceTests
{
    private static AppDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new AppDbContext(options);
    }

    private static CreatePatientRequest MinimalRequest(string name, string phone = "") => new()
    {
        Name = name,
        Gender = "Female",
        Phone = string.IsNullOrEmpty(phone) ? null : phone,
    };

    [Fact]
    public async Task Create_persists_and_can_be_read_back_with_every_field()
    {
        await using var db = CreateContext(nameof(Create_persists_and_can_be_read_back_with_every_field));
        var service = new PatientService(db);

        var created = await service.CreateAsync(new CreatePatientRequest
        {
            Name = "Alice",
            Age = 34,
            DateOfBirth = new DateOnly(1990, 5, 14),
            Gender = "Female",
            Phone = "9876543210",
            Allergies = "Penicillin",
            CurrentMedications = "Metformin",
            ChronicConditions = "Type 2 diabetes",
            EmergencyContactName = "Bob",
            EmergencyContactPhone = "9876500000",
        });

        var fetched = await service.GetAsync(created.PatientId);

        Assert.NotNull(fetched);
        Assert.Equal("Alice", fetched!.Name);
        Assert.Equal(34, fetched.Age);
        Assert.Equal(new DateOnly(1990, 5, 14), fetched.DateOfBirth);
        Assert.Equal("Female", fetched.Gender);
        Assert.Equal("9876543210", fetched.Phone);
        Assert.Equal("Penicillin", fetched.Allergies);
        Assert.Equal("Metformin", fetched.CurrentMedications);
        Assert.Equal("Type 2 diabetes", fetched.ChronicConditions);
        Assert.Equal("Bob", fetched.EmergencyContactName);
        Assert.Equal("9876500000", fetched.EmergencyContactPhone);
    }

    [Fact]
    public async Task Get_returns_null_for_an_unknown_patient_id()
    {
        await using var db = CreateContext(nameof(Get_returns_null_for_an_unknown_patient_id));
        var service = new PatientService(db);

        Assert.Null(await service.GetAsync(999));
    }

    [Fact]
    public async Task Two_patients_may_share_the_same_phone_number()
    {
        await using var db = CreateContext(nameof(Two_patients_may_share_the_same_phone_number));
        var service = new PatientService(db);

        await service.CreateAsync(MinimalRequest("Alice", "9876543210"));
        await service.CreateAsync(MinimalRequest("Bob", "9876543210"));

        var results = await service.SearchAsync(null, "9876543210");
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task Update_modifies_the_existing_patient_and_persists()
    {
        await using var db = CreateContext(nameof(Update_modifies_the_existing_patient_and_persists));
        var service = new PatientService(db);
        var created = await service.CreateAsync(MinimalRequest("Alice"));

        var updated = await service.UpdateAsync(created.PatientId, new UpdatePatientRequest
        {
            Name = "Alice Renamed",
            Gender = "Female",
            Phone = "111",
            EmergencyContactName = "Carol",
            EmergencyContactPhone = "222",
        });

        Assert.NotNull(updated);
        Assert.Equal("Alice Renamed", updated!.Name);
        Assert.Equal("111", updated.Phone);
        Assert.Equal("Carol", updated.EmergencyContactName);
        Assert.Equal("222", updated.EmergencyContactPhone);

        var refetched = await service.GetAsync(created.PatientId);
        Assert.Equal("Alice Renamed", refetched!.Name);
    }

    [Fact]
    public async Task Update_returns_null_for_an_unknown_patient_id()
    {
        await using var db = CreateContext(nameof(Update_returns_null_for_an_unknown_patient_id));
        var service = new PatientService(db);

        var result = await service.UpdateAsync(999, new UpdatePatientRequest { Name = "Nobody", Gender = "Unknown" });

        Assert.Null(result);
    }

    [Fact]
    public async Task Search_matches_a_substring_occurring_anywhere_in_the_name_not_only_a_prefix()
    {
        await using var db = CreateContext(nameof(Search_matches_a_substring_occurring_anywhere_in_the_name_not_only_a_prefix));
        var service = new PatientService(db);
        await service.CreateAsync(MinimalRequest("Alexandra Smith"));
        await service.CreateAsync(MinimalRequest("Bob Jones"));

        // "andra" occurs mid-string in "Alexandra" -- a prefix-only match would miss this.
        var results = await service.SearchAsync("andra", null);

        Assert.Single(results);
        Assert.Equal("Alexandra Smith", results[0].Name);
    }

    [Fact]
    public async Task Search_matches_a_substring_occurring_anywhere_in_the_phone_not_only_a_prefix()
    {
        await using var db = CreateContext(nameof(Search_matches_a_substring_occurring_anywhere_in_the_phone_not_only_a_prefix));
        var service = new PatientService(db);
        await service.CreateAsync(MinimalRequest("Alice", "9876543210"));
        await service.CreateAsync(MinimalRequest("Bob", "1112223333"));

        var results = await service.SearchAsync(null, "6543");

        Assert.Single(results);
        Assert.Equal("Alice", results[0].Name);
    }

    [Fact]
    public async Task Search_is_case_insensitive()
    {
        await using var db = CreateContext(nameof(Search_is_case_insensitive));
        var service = new PatientService(db);
        await service.CreateAsync(MinimalRequest("Alexandra Smith"));

        var results = await service.SearchAsync("ALEXANDRA", null);

        Assert.Single(results);
    }

    [Fact]
    public async Task Search_with_neither_name_nor_phone_returns_no_results()
    {
        await using var db = CreateContext(nameof(Search_with_neither_name_nor_phone_returns_no_results));
        var service = new PatientService(db);
        await service.CreateAsync(MinimalRequest("Alice"));

        var results = await service.SearchAsync(null, null);

        Assert.Empty(results);
    }
}
