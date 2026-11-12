using System.Text.Json;
using ARGOS.Domain.Aggregates.Organization.ValueObjects;
using ARGOS.Domain.Aggregates.OrganizationSetup;
using ARGOS.Domain.Aggregates.OrganizationSetup.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ARGOS.Infrastructure.Persistence.Configurations;

public sealed class OrganizationSetupConfiguration : IEntityTypeConfiguration<OrganizationSetup>
{
    public void Configure(EntityTypeBuilder<OrganizationSetup> builder)
    {
        builder.ToTable("OrganizationSetups");

        builder.HasKey(setup => setup.Id);
        builder.Property(setup => setup.Id)
            .HasConversion(id => id.Value, value => OrganizationSetupId.From(value))
            .ValueGeneratedNever();

        builder.Property(setup => setup.OrganizationId)
            .HasConversion(id => id.Value, value => OrganizationId.From(value))
            .IsRequired();

        builder.HasIndex(setup => new { setup.OrganizationId, setup.Version })
            .IsUnique();

        builder.Property(setup => setup.PreviousVersionId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value,
                value => value == null ? null : OrganizationSetupId.From(value.Value));
        builder.Property(setup => setup.NextVersionId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value,
                value => value == null ? null : OrganizationSetupId.From(value.Value));

        builder.Property(setup => setup.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        ConfigureAuditFields(builder);
        ConfigureTerms(builder);
        ConfigureUnits(builder);
        ConfigureCompetences(builder);
        ConfigureActingCapacities(builder);
        ConfigurePositions(builder);

        builder.Ignore(setup => setup.DomainEvents);
        builder.Ignore(setup => setup.HasDomainEvents);
    }

    private static void ConfigureAuditFields(EntityTypeBuilder<OrganizationSetup> builder)
    {
        builder.Property(setup => setup.CreatedByReference).HasMaxLength(100).IsRequired();
        builder.Property(setup => setup.CreationPolicyVersion).HasMaxLength(100).IsRequired();
        builder.Property(setup => setup.CreationAuthorizationReference).HasMaxLength(200).IsRequired();
        builder.Property(setup => setup.PublishedByReference).HasMaxLength(100);
        builder.Property(setup => setup.PublicationAuthorizationReference).HasMaxLength(200);
    }

    private static void ConfigureTerms(EntityTypeBuilder<OrganizationSetup> builder)
    {
        builder.OwnsMany(setup => setup.Terms, term =>
        {
            term.ToTable("OrganizationSetupTerms");
            term.WithOwner().HasForeignKey("OrganizationSetupId");
            term.HasKey("OrganizationSetupId", nameof(OrganizationTerm.Concept));
            term.Property(item => item.Concept).HasConversion<string>().HasMaxLength(40);
            term.Property(item => item.Singular).HasMaxLength(200).IsRequired();
            term.Property(item => item.Plural).HasMaxLength(200).IsRequired();
        });
        builder.Navigation(setup => setup.Terms).UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigureUnits(EntityTypeBuilder<OrganizationSetup> builder)
    {
        builder.OwnsMany(setup => setup.Units, unit =>
        {
            unit.ToTable("OrganizationSetupUnits");
            unit.WithOwner().HasForeignKey("OrganizationSetupId");
            unit.HasKey("OrganizationSetupId", nameof(OrganizationUnitDefinition.Id));
            unit.Property(item => item.Id).ValueGeneratedNever();
            unit.Property(item => item.Code).HasMaxLength(50).IsRequired();
            unit.Property(item => item.Name).HasMaxLength(200).IsRequired();
            unit.HasIndex("OrganizationSetupId", nameof(OrganizationUnitDefinition.Code)).IsUnique();
        });
        builder.Navigation(setup => setup.Units).UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigureCompetences(EntityTypeBuilder<OrganizationSetup> builder)
    {
        builder.OwnsMany(setup => setup.Competences, competence =>
        {
            competence.ToTable("OrganizationSetupCompetences");
            competence.WithOwner().HasForeignKey("OrganizationSetupId");
            competence.HasKey("OrganizationSetupId", nameof(CompetenceDefinition.Id));
            competence.Property(item => item.Id).ValueGeneratedNever();
            competence.Property(item => item.Code).HasMaxLength(50).IsRequired();
            competence.Property(item => item.Name).HasMaxLength(200).IsRequired();
            competence.Property(item => item.Description).HasMaxLength(2000);
            competence.HasIndex("OrganizationSetupId", nameof(CompetenceDefinition.Code)).IsUnique();
        });
        builder.Navigation(setup => setup.Competences).UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigureActingCapacities(EntityTypeBuilder<OrganizationSetup> builder)
    {
        builder.OwnsMany(setup => setup.ActingCapacities, capacity =>
        {
            capacity.ToTable("OrganizationSetupActingCapacities");
            capacity.WithOwner().HasForeignKey("OrganizationSetupId");
            capacity.HasKey("OrganizationSetupId", nameof(ActingCapacityDefinition.Id));
            capacity.Property(item => item.Id).ValueGeneratedNever();
            capacity.Property(item => item.Code).HasMaxLength(50).IsRequired();
            capacity.Property(item => item.Name).HasMaxLength(200).IsRequired();
            capacity.Property(item => item.Description).HasMaxLength(2000);
            capacity.HasIndex("OrganizationSetupId", nameof(ActingCapacityDefinition.Code)).IsUnique();
        });
        builder.Navigation(setup => setup.ActingCapacities).UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigurePositions(EntityTypeBuilder<OrganizationSetup> builder)
    {
        builder.OwnsMany(setup => setup.Positions, position =>
        {
            position.ToTable("OrganizationSetupPositions");
            position.WithOwner().HasForeignKey("OrganizationSetupId");
            position.HasKey("OrganizationSetupId", nameof(InstitutionalPositionDefinition.Id));
            position.Property(item => item.Id).ValueGeneratedNever();
            position.Property(item => item.Code).HasMaxLength(50).IsRequired();
            position.Property(item => item.Name).HasMaxLength(200).IsRequired();
            position.HasIndex("OrganizationSetupId", nameof(InstitutionalPositionDefinition.Code)).IsUnique();

            ConfigureGuidList(position, "_competenceIds", "CompetenceIds");
            ConfigureGuidList(position, "_actingCapacityIds", "ActingCapacityIds");
        });
        builder.Navigation(setup => setup.Positions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigureGuidList(
        OwnedNavigationBuilder<OrganizationSetup, InstitutionalPositionDefinition> builder,
        string fieldName,
        string columnName)
    {
        var property = builder.Property<List<Guid>>(fieldName)
            .HasColumnName(columnName)
            .HasConversion(
                value => SerializeGuidList(value),
                value => DeserializeGuidList(value))
            .IsRequired();

        property.Metadata.SetValueComparer(new ValueComparer<List<Guid>>(
            (left, right) => ReferenceEquals(left, right)
                || (left != null && right != null && left.SequenceEqual(right)),
            value => value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)),
            value => value.ToList()));
    }

    private static string SerializeGuidList(List<Guid> value)
        => JsonSerializer.Serialize(value);

    private static List<Guid> DeserializeGuidList(string value)
        => JsonSerializer.Deserialize<List<Guid>>(value) ?? [];
}
