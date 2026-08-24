using Microsoft.EntityFrameworkCore;
using PatientManagement.Domain.Models;

namespace PatientManagement.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<DoctorDetails> DoctorDetails => Set<DoctorDetails>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<Visit> Visits => Set<Visit>();
    public DbSet<Prescription> Prescriptions => Set<Prescription>();
    public DbSet<PrescriptionItem> PrescriptionItems => Set<PrescriptionItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Users: exactly Id, Username, Password -- no extra auth columns
        // (no PasswordHash/Salt/MfaSecret). See implementation-brd.md's
        // fixed Authentication spec.
        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(u => u.Username).IsRequired();
            entity.Property(u => u.Password).IsRequired();
        });

        modelBuilder.Entity<DoctorDetails>(entity =>
        {
            entity.Property(d => d.ClinicName).IsRequired();
            entity.Property(d => d.DoctorName).IsRequired();
        });

        modelBuilder.Entity<Patient>(entity =>
        {
            // SQL Server's default IDENTITY seed is 1, not 0 -- the fixed
            // spec requires PatientId to start at 0. Must be set explicitly.
            entity.Property(p => p.PatientId).UseIdentityColumn(seed: 0, increment: 1);
            entity.Property(p => p.Name).IsRequired();
            entity.Property(p => p.Gender).IsRequired();
            // Phone is deliberately NOT configured as unique or required --
            // multiple patients may legitimately share a phone number.
        });

        modelBuilder.Entity<Appointment>(entity =>
        {
            // Only one appointment may exist for a given date/time slot --
            // enforced at the DB level so it holds regardless of which
            // future code path (scheduled booking or walk-in) inserts the row.
            entity.HasIndex(a => a.ScheduledTime).IsUnique();

            entity.HasOne(a => a.Patient)
                  .WithMany()
                  .HasForeignKey(a => a.PatientId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Visit>(entity =>
        {
            entity.Property(v => v.Weight).HasColumnType("decimal(6,3)");
            entity.Property(v => v.Temperature).HasColumnType("decimal(4,1)");

            // A visit always originates from exactly one appointment, and an
            // appointment produces at most one visit.
            entity.HasOne(v => v.Appointment)
                  .WithOne()
                  .HasForeignKey<Visit>(v => v.AppointmentId)
                  .IsRequired()
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(v => v.AppointmentId).IsUnique();

            entity.HasOne(v => v.Patient)
                  .WithMany()
                  .HasForeignKey(v => v.PatientId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Prescription>(entity =>
        {
            entity.Property(p => p.ClinicName).IsRequired();
            entity.Property(p => p.DoctorName).IsRequired();

            entity.HasOne(p => p.Visit)
                  .WithMany()
                  .HasForeignKey(p => p.VisitId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PrescriptionItem>(entity =>
        {
            entity.Property(i => i.DrugName).IsRequired();

            entity.HasOne(i => i.Prescription)
                  .WithMany(p => p.Items)
                  .HasForeignKey(i => i.PrescriptionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
