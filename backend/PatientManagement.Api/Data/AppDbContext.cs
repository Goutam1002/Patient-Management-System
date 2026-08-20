using Microsoft.EntityFrameworkCore;
using PatientManagement.Api.Models;

namespace PatientManagement.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<DoctorDetails> DoctorDetails => Set<DoctorDetails>();
    public DbSet<Patient> Patients => Set<Patient>();

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
    }
}
