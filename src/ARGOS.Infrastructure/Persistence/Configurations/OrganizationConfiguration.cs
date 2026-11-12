using ARGOS.Domain.Aggregates.Organization;
using ARGOS.Domain.Aggregates.Organization.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ARGOS.Infrastructure.Persistence.Configurations;

public sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("Organizations");

        builder.HasKey(organization => organization.Id);
        builder.Property(organization => organization.Id)
            .HasConversion(id => id.Value, value => OrganizationId.From(value))
            .ValueGeneratedNever();

        builder.Property(organization => organization.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(organization => organization.Code)
            .HasMaxLength(50)
            .IsRequired();
        builder.HasIndex(organization => organization.Code).IsUnique();

        builder.Property(organization => organization.Description)
            .HasMaxLength(2000);

        builder.Property(organization => organization.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Ignore(organization => organization.DomainEvents);
        builder.Ignore(organization => organization.HasDomainEvents);
    }
}
