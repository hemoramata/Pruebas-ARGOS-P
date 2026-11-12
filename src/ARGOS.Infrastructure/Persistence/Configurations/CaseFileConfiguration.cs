using ARGOS.Domain.Aggregates.CaseFile;
using ARGOS.Domain.Aggregates.CaseFile.ValueObjects;
using ARGOS.Domain.Aggregates.Evidence.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ARGOS.Infrastructure.Persistence.Configurations;

public sealed class CaseFileConfiguration : IEntityTypeConfiguration<CaseFile>
{
    public void Configure(EntityTypeBuilder<CaseFile> builder)
    {
        builder.ToTable("CaseFiles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Number)
            .HasConversion(number => number.Value, value => CaseFileNumber.Create(value).Value)
            .HasMaxLength(100)
            .IsRequired();
        builder.HasIndex(x => x.Number).IsUnique();
        builder.Property(x => x.Status)
            .IsConcurrencyToken()
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();
        builder.Property(x => x.WaitingReason).HasMaxLength(2000);
        builder.Property(x => x.FinalResultCode).HasMaxLength(100);
        builder.Property(x => x.SubjectTypeCode).HasMaxLength(100);
        builder.Property(x => x.SubjectReference).HasMaxLength(200);
        builder.HasMany(x => x.EvidenceLinks)
            .WithOne()
            .HasForeignKey("CaseFileId")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Metadata.FindNavigation(nameof(CaseFile.EvidenceLinks))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class CaseFileEvidenceConfiguration : IEntityTypeConfiguration<CaseFileEvidence>
{
    public void Configure(EntityTypeBuilder<CaseFileEvidence> builder)
    {
        builder.ToTable("CaseFileEvidenceLinks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.EvidenceId)
            .HasConversion(id => id.Value, value => EvidenceId.From(value))
            .HasMaxLength(40)
            .IsRequired();
        builder.Property(x => x.RequirementCode).HasMaxLength(100).IsRequired();
        builder.Property(x => x.PolicyVersion).HasMaxLength(100).IsRequired();
        builder.Property(x => x.LinkedByPositionId).IsRequired();
        builder.Property(x => x.ProcedureVersionId).IsRequired();
        builder.Property(x => x.ValidatedAtUtc).IsRequired();
        builder.Property(x => x.LinkedAtUtc).IsRequired();
        builder.HasIndex("CaseFileId", nameof(CaseFileEvidence.EvidenceId),
            nameof(CaseFileEvidence.RequirementCode)).IsUnique();
    }
}
