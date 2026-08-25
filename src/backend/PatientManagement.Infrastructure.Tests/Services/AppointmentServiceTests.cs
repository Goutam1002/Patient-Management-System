using Microsoft.EntityFrameworkCore;
using PatientManagement.Application.DTOs;
using PatientManagement.Application.Services;
using PatientManagement.Domain.Models;
using PatientManagement.Infrastructure.Data;
using PatientManagement.Infrastructure.Services;
using Xunit;

namespace PatientManagement.Infrastructure.Tests.Services;

public class AppointmentServiceTests
{
    private static AppDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<Patient> AddPatientAsync(AppDbContext db, string name)
    {
        var patient = new Patient { Name = name, Gender = "Female" };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();
        return patient;
    }

    [Fact]
    public async Task Create_persists_the_duration_exactly_as_entered_and_never_a_fixed_default()
    {
        await using var db = CreateContext(nameof(Create_persists_the_duration_exactly_as_entered_and_never_a_fixed_default));
        var service = new AppointmentService(db);
        var patient = await AddPatientAsync(db, "Alice");

        var first = await service.CreateAsync(new CreateAppointmentRequest
        {
            PatientId = patient.PatientId,
            ScheduledTime = new DateTime(2026, 3, 2, 9, 0, 0),
            DurationMinutes = 45,
        });
        var second = await service.CreateAsync(new CreateAppointmentRequest
        {
            PatientId = patient.PatientId,
            ScheduledTime = new DateTime(2026, 3, 2, 11, 0, 0),
            DurationMinutes = 7,
        });

        // Two different doctor-entered durations survive unchanged -- no single
        // system-wide value is being applied to either.
        Assert.Equal(45, first!.DurationMinutes);
        Assert.Equal(7, second!.DurationMinutes);
        Assert.Equal(45, (await db.Appointments.SingleAsync(a => a.Id == first.Id)).DurationMinutes);
        Assert.Equal(7, (await db.Appointments.SingleAsync(a => a.Id == second.Id)).DurationMinutes);
    }

    [Fact]
    public async Task Create_defaults_a_new_appointment_to_scheduled_status()
    {
        await using var db = CreateContext(nameof(Create_defaults_a_new_appointment_to_scheduled_status));
        var service = new AppointmentService(db);
        var patient = await AddPatientAsync(db, "Alice");

        var created = await service.CreateAsync(new CreateAppointmentRequest
        {
            PatientId = patient.PatientId,
            ScheduledTime = new DateTime(2026, 3, 2, 9, 0, 0),
            DurationMinutes = 15,
        });

        Assert.Equal(AppointmentStatus.Scheduled, created!.Status);
        Assert.Null(created.VisitId);
        Assert.Equal("Alice", created.PatientName);
    }

    [Fact]
    public async Task Create_for_an_unknown_patient_returns_null()
    {
        await using var db = CreateContext(nameof(Create_for_an_unknown_patient_returns_null));
        var service = new AppointmentService(db);

        var created = await service.CreateAsync(new CreateAppointmentRequest
        {
            PatientId = 4242,
            ScheduledTime = new DateTime(2026, 3, 2, 9, 0, 0),
            DurationMinutes = 15,
        });

        Assert.Null(created);
        Assert.Empty(db.Appointments);
    }

    [Fact]
    public async Task Create_rejects_a_second_appointment_in_an_already_occupied_slot()
    {
        await using var db = CreateContext(nameof(Create_rejects_a_second_appointment_in_an_already_occupied_slot));
        var service = new AppointmentService(db);
        var alice = await AddPatientAsync(db, "Alice");
        var bob = await AddPatientAsync(db, "Bob");
        var slot = new DateTime(2026, 3, 2, 9, 0, 0);

        await service.CreateAsync(new CreateAppointmentRequest
        {
            PatientId = alice.PatientId,
            ScheduledTime = slot,
            DurationMinutes = 15,
        });

        await Assert.ThrowsAsync<AppointmentSlotConflictException>(() => service.CreateAsync(new CreateAppointmentRequest
        {
            PatientId = bob.PatientId,
            ScheduledTime = slot,
            DurationMinutes = 30,
        }));

        Assert.Single(db.Appointments); // the rejected attempt inserted nothing
    }

    [Fact]
    public async Task Daily_list_merges_scheduled_and_walk_in_entries_ordered_by_time()
    {
        await using var db = CreateContext(nameof(Daily_list_merges_scheduled_and_walk_in_entries_ordered_by_time));
        var service = new AppointmentService(db);
        var alice = await AddPatientAsync(db, "Alice");
        var bob = await AddPatientAsync(db, "Bob");
        var day = new DateOnly(2026, 3, 2);

        // Inserted out of order, and the walk-in row (an Appointment already
        // carrying a Visit) sits between two scheduled ones.
        await service.CreateAsync(new CreateAppointmentRequest
        {
            PatientId = alice.PatientId,
            ScheduledTime = day.ToDateTime(new TimeOnly(15, 0)),
            DurationMinutes = 15,
        });
        await service.CreateAsync(new CreateAppointmentRequest
        {
            PatientId = bob.PatientId,
            ScheduledTime = day.ToDateTime(new TimeOnly(9, 0)),
            DurationMinutes = 20,
        });

        var walkInAppointment = new Appointment
        {
            PatientId = bob.PatientId,
            ScheduledTime = day.ToDateTime(new TimeOnly(11, 30)),
            DurationMinutes = 10,
            Status = AppointmentStatus.Completed,
        };
        db.Appointments.Add(walkInAppointment);
        await db.SaveChangesAsync();
        db.Visits.Add(new Visit
        {
            PatientId = bob.PatientId,
            AppointmentId = walkInAppointment.Id,
            VisitNumber = 1,
        });
        await db.SaveChangesAsync();

        // A different day's appointment must not leak into the list.
        await service.CreateAsync(new CreateAppointmentRequest
        {
            PatientId = alice.PatientId,
            ScheduledTime = day.AddDays(1).ToDateTime(new TimeOnly(9, 0)),
            DurationMinutes = 15,
        });

        var daily = await service.GetDailyAsync(day);

        Assert.Equal(3, daily.Count);
        Assert.Equal(
            new[] { new TimeOnly(9, 0), new TimeOnly(11, 30), new TimeOnly(15, 0) },
            daily.Select(a => TimeOnly.FromDateTime(a.ScheduledTime)).ToArray());
        // The walk-in entry is in the same list, not a separate one, and is
        // identifiable by already having a visit attached.
        Assert.Null(daily[0].VisitId);
        Assert.NotNull(daily[1].VisitId);
        Assert.Equal(AppointmentStatus.Completed, daily[1].Status);
        Assert.Null(daily[2].VisitId);
    }

    [Theory]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.NoShow)]
    [InlineData(AppointmentStatus.Scheduled)]
    public async Task Update_status_accepts_every_status_except_completed(AppointmentStatus target)
    {
        await using var db = CreateContext($"{nameof(Update_status_accepts_every_status_except_completed)}_{target}");
        var service = new AppointmentService(db);
        var patient = await AddPatientAsync(db, "Alice");
        var created = await service.CreateAsync(new CreateAppointmentRequest
        {
            PatientId = patient.PatientId,
            ScheduledTime = new DateTime(2026, 3, 2, 9, 0, 0),
            DurationMinutes = 15,
        });

        var updated = await service.UpdateStatusAsync(created!.Id, target);

        Assert.Equal(target, updated!.Status);
        Assert.Equal(target, (await db.Appointments.SingleAsync(a => a.Id == created.Id)).Status);
    }

    [Fact]
    public async Task Update_status_to_completed_is_rejected_and_leaves_the_appointment_untouched()
    {
        await using var db = CreateContext(nameof(Update_status_to_completed_is_rejected_and_leaves_the_appointment_untouched));
        var service = new AppointmentService(db);
        var patient = await AddPatientAsync(db, "Alice");
        var created = await service.CreateAsync(new CreateAppointmentRequest
        {
            PatientId = patient.PatientId,
            ScheduledTime = new DateTime(2026, 3, 2, 9, 0, 0),
            DurationMinutes = 15,
        });

        // FLAGGED ASSUMPTION: Completed is reachable only via visit creation.
        var exception = await Assert.ThrowsAsync<AppointmentStatusTransitionException>(
            () => service.UpdateStatusAsync(created!.Id, AppointmentStatus.Completed));

        Assert.Equal(AppointmentStatus.Completed, exception.RequestedStatus);
        Assert.Equal(
            AppointmentStatus.Scheduled,
            (await db.Appointments.SingleAsync(a => a.Id == created!.Id)).Status);
    }

    [Fact]
    public async Task Update_status_for_an_unknown_appointment_returns_null()
    {
        await using var db = CreateContext(nameof(Update_status_for_an_unknown_appointment_returns_null));
        var service = new AppointmentService(db);

        var updated = await service.UpdateStatusAsync(4242, AppointmentStatus.Cancelled);

        Assert.Null(updated);
    }
}
