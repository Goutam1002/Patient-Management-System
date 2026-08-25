using Microsoft.EntityFrameworkCore;
using PatientManagement.Application.Services;
using PatientManagement.Infrastructure.Data;

namespace PatientManagement.Infrastructure.Services;

/// <summary>
/// The single service-layer double-booking pre-check, shared by every path
/// that inserts an Appointment (scheduled booking and walk-in registration
/// alike) so the rule cannot drift between them. The unique index on
/// Appointment.ScheduledTime is the real backstop; this exists so callers get
/// a named AppointmentSlotConflictException instead of a raw SQL constraint
/// violation.
/// </summary>
internal static class AppointmentSlotGuard
{
    public static async Task EnsureSlotIsFreeAsync(AppDbContext db, DateTime scheduledTime)
    {
        var slotTaken = await db.Appointments.AnyAsync(a => a.ScheduledTime == scheduledTime);
        if (slotTaken)
        {
            throw new AppointmentSlotConflictException(scheduledTime);
        }
    }
}
