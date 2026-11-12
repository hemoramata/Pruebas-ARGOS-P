using ARGOS.Domain.Aggregates.Personnel;
using Microsoft.EntityFrameworkCore;

namespace ARGOS.Infrastructure.Persistence;

public sealed class DesignationDbContext(DbContextOptions<DesignationDbContext> options) : DbContext(options)
{
    public DbSet<AdministratorDesignation> Designations => Set<AdministratorDesignation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<AdministratorDesignation>();
        entity.ToTable("AdministratorDesignations");
        entity.HasKey(d => d.Id);
        entity.Property(d => d.Id).ValueGeneratedNever();
        entity.HasIndex(d => new { d.OrganizationId, d.PersonId, d.AssignmentId });
        entity.Property(d => d.AuthorizationReference).HasMaxLength(200).IsRequired();
        entity.Property(d => d.AuthorizedBy).HasMaxLength(200).IsRequired();
        entity.Property(d => d.RecordedBy).HasMaxLength(200).IsRequired();
        entity.Property(d => d.RevocationReference).HasMaxLength(200);
        entity.Property(d => d.RevokedAt).IsConcurrencyToken();
    }

    public Task InitializeSchemaAsync(CancellationToken ct = default) => Database.MigrateAsync(ct);
}