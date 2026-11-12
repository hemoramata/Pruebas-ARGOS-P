using System.Text.Json;
using ARGOS.Domain.Aggregates.Organization.ValueObjects;
using ARGOS.Domain.Aggregates.Personnel;
using Microsoft.EntityFrameworkCore;

namespace ARGOS.Infrastructure.Persistence;

/// <summary>Personnel tables share the institutional PostgreSQL database; no client-side storage.</summary>
public sealed class PersonnelDbContext(DbContextOptions<PersonnelDbContext> options) : DbContext(options)
{
    public DbSet<InstitutionalPerson> People => Set<InstitutionalPerson>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var person = modelBuilder.Entity<InstitutionalPerson>();
        person.ToTable("PersonnelPeople");
        person.HasKey(p => p.Id);
        person.Property(p => p.Id).ValueGeneratedNever();
        person.Property(p => p.OrganizationId).HasConversion(id => id.Value, value => OrganizationId.From(value));
        person.Property(p => p.FullName).HasMaxLength(200).IsRequired();
        person.Property(p => p.Identity).HasConversion(
            identity => JsonSerializer.Serialize(identity, (JsonSerializerOptions?)null),
            json => JsonSerializer.Deserialize<OfficialIdentity>(json, (JsonSerializerOptions?)null)!);
        person.HasIndex(p => new { p.OrganizationId, p.Identity }).IsUnique();
        person.Property<long>("Revision").IsConcurrencyToken();
        person.OwnsMany(p => p.Assignments, assignment =>
        {
            assignment.ToTable("PersonnelAssignments");
            assignment.WithOwner().HasForeignKey(a => a.PersonId);
            assignment.HasKey(a => a.Id);
            assignment.Property(a => a.Id).ValueGeneratedNever();
            assignment.Property(a => a.PositionId);
            assignment.Property(a => a.StartsAt);
            assignment.Property(a => a.EndsAt);
            assignment.Property(a => a.AuthorizationReference).IsRequired();
            assignment.Property(a => a.RevokedAt);
            assignment.Property(a => a.RevocationAuthorizationReference);
        });
        person.Navigation(p => p.Assignments).HasField("assignments").UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ChangeTracker.DetectChanges();
        var changedOwners = ChangeTracker.Entries<InstitutionalAssignment>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(e => e.Entity.PersonId).ToHashSet();
        foreach (var entry in ChangeTracker.Entries<InstitutionalPerson>())
        {
            if (entry.State == EntityState.Modified || changedOwners.Contains(entry.Entity.Id))
            {
                var revision = entry.Property<long>("Revision");
                revision.CurrentValue = checked(revision.CurrentValue + 1);
            }
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
