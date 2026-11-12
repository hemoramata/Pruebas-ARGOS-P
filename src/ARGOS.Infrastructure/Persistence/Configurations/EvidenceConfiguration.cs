using ARGOS.Domain.Aggregates.Evidence;
using ARGOS.Domain.Aggregates.Evidence.Enums;
using ARGOS.Domain.Aggregates.Evidence.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ARGOS.Infrastructure.Persistence.Configurations;

public sealed class EvidenceConfiguration : IEntityTypeConfiguration<Evidence>
{
    public void Configure(EntityTypeBuilder<Evidence> builder)
    {
        builder.ToTable("Evidences");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasConversion(id => id.Value, value => EvidenceId.From(value))
            .ValueGeneratedNever();
        builder.Property(x => x.CustodianOrganizationId).IsRequired();
        builder.Property(x => x.EvidenceTypeCode).HasMaxLength(100).IsRequired();
        builder.Property(x => x.SubjectTypeCode).HasMaxLength(100).IsRequired();
        builder.Property(x => x.SubjectReference).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.HasMany(x => x.Sources)
            .WithOne()
            .HasForeignKey("EvidenceId")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Metadata.FindNavigation(nameof(Evidence.Sources))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class EvidenceSourceConfiguration : IEntityTypeConfiguration<EvidenceSource>
{
    public void Configure(EntityTypeBuilder<EvidenceSource> builder)
    {
        builder.ToTable("EvidenceSources");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasConversion(id => id.Value, value => EvidenceSourceId.From(value))
            .ValueGeneratedNever();
        builder.Property(x => x.SourceTypeCode).HasMaxLength(100).IsRequired();
        builder.Property(x => x.SourceReference).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Role).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ProducedAtUtc).IsRequired();
        builder.Property(x => x.ReceivedAtUtc).IsRequired();
        builder.Property(x => x.Channel).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ReceivedByPositionId);
    }
}
