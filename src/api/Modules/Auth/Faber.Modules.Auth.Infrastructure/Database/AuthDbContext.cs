using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Faber.Modules.Auth.Infrastructure.Database;

public class AuthDbContext(DbContextOptions<AuthDbContext> options) : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<AuthSession> Sessions => Set<AuthSession>();

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DbConstants.SchemaName);

        modelBuilder.Entity<AuthSession>(session =>
        {
            session.ToTable("sessions");
            session.HasKey(s => s.Id);
            session.Property(s => s.Id).HasMaxLength(DbConstants.SessionKeyMaxLength);
            session.Property(s => s.Subject).HasMaxLength(DbConstants.KeycloakIdMaxLength).IsRequired();
            session.Property(s => s.KeycloakSessionId).HasMaxLength(DbConstants.KeycloakIdMaxLength);
            session.Property(s => s.Ticket).IsRequired();
            session.HasIndex(s => s.KeycloakSessionId);
            session.HasIndex(s => s.Subject);
            session.HasIndex(s => s.ExpiresAt);
        });

        modelBuilder.Entity<DataProtectionKey>().ToTable("data_protection_keys");
    }
}
