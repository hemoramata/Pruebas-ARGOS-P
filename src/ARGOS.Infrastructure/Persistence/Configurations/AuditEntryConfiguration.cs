using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ARGOS.Infrastructure.Persistence.Configurations;

public sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("AuditEntries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.OrganizationId);
        builder.Property(x => x.ActorReference).HasMaxLength(200).IsRequired();
        builder.Property(x => x.SessionReference).HasMaxLength(200);
        builder.Property(x => x.ActionCode).HasMaxLength(120).IsRequired();
        builder.Property(x => x.SubjectType).HasMaxLength(120).IsRequired();
        builder.Property(x => x.SubjectReference).HasMaxLength(240).IsRequired();
        builder.Property(x => x.Outcome).HasMaxLength(40).IsRequired();
        builder.Property(x => x.OccurredAtUtc).IsRequired();
        builder.Property(x => x.RequestReference).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Details).HasMaxLength(4000);
        builder.HasIndex(x => new { x.OrganizationId, x.OccurredAtUtc });
        builder.HasIndex(x => new { x.SubjectType, x.SubjectReference });
    }
}
