using Microsoft.EntityFrameworkCore;
using PatientManagement.Api.Models;

namespace PatientManagement.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

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
    }
}
