using ARGOS.Domain.Aggregates.Procedure;
using ARGOS.Domain.Aggregates.Procedure.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ARGOS.Infrastructure.Persistence.Configurations;

public sealed class ProcedureConfiguration : IEntityTypeConfiguration<Procedure>
{
    public void Configure(EntityTypeBuilder<Procedure> builder)
    {
        builder.ToTable("Procedures");

        builder.HasKey(procedure => procedure.Id);

        builder.Property(procedure => procedure.Id)
            .HasConversion(
                id => id.Value,
                value => ProcedureId.From(value))
            .ValueGeneratedNever();

        builder.Property(procedure => procedure.DefinitionId)
            .HasConversion(
                id => id.Value,
                value => ProcedureDefinitionId.From(value))
            .IsRequired();

        builder.Property(procedure => procedure.Version)
            .IsRequired();

        builder.Property(procedure => procedure.PreviousVersionId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value,
                value => value == null ? null : ProcedureId.From(value.Value));

        builder.Property(procedure => procedure.NextVersionId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value,
                value => value == null ? null : ProcedureId.From(value.Value));

        builder.Property(procedure => procedure.Code)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(procedure => procedure.Code);

        builder.HasIndex(procedure => new
        {
            procedure.Code,
            procedure.Version
        })
            .IsUnique();

        builder.HasIndex(procedure => new
        {
            procedure.DefinitionId,
            procedure.Version
        })
            .IsUnique();

        builder.Property(procedure => procedure.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(procedure => procedure.Description)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(procedure => procedure.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(procedure => procedure.ApprovalPolicyVersion)
            .HasMaxLength(100);

        builder.Property(procedure => procedure.RequiredApprovalCount);

        builder.OwnsMany(procedure => procedure.Decisions, decision =>
        {
            decision.ToTable("ProcedureDecisions");
            decision.WithOwner().HasForeignKey("ProcedureId");
            decision.HasKey(item => item.Id);

            decision.Property(item => item.Id)
                .ValueGeneratedNever();

            decision.Property(item => item.Type)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();

            decision.Property(item => item.ActorReference)
                .HasMaxLength(100)
                .IsRequired();

            decision.Property(item => item.AssignmentReference)
                .HasMaxLength(100)
                .IsRequired();

            decision.Property(item => item.PositionReference)
                .HasMaxLength(100)
                .IsRequired();

            decision.Property(item => item.CompetenceCode)
                .HasMaxLength(100)
                .IsRequired();

            decision.Property(item => item.PolicyVersion)
                .HasMaxLength(100)
                .IsRequired();

            decision.Property(item => item.AuthorizationReference)
                .HasMaxLength(200)
                .IsRequired();

            decision.Property(item => item.Comments)
                .HasMaxLength(2000);

            decision.Property(item => item.OccurredAtUtc)
                .IsRequired();

            decision.HasIndex("ProcedureId", nameof(ProcedureDecision.OccurredAtUtc));
        });

        builder.Navigation(procedure => procedure.Decisions)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(procedure => procedure.DomainEvents);
        builder.Ignore(procedure => procedure.HasDomainEvents);
    }
}
