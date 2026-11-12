using ARGOS.Domain.Aggregates.Workflow;
using ARGOS.Domain.Aggregates.Workflow.ValueObjects;
using ARGOS.Domain.Aggregates.Workflow.Activities.ValueObjects;
using ARGOS.Domain.Aggregates.Workflow.Transitions.ValueObjects;
using ARGOS.Domain.Aggregates.Workflow.Conditions.ValueObjects;
using ARGOS.Domain.Aggregates.Procedure;
using ARGOS.Domain.Aggregates.Procedure.ValueObjects;
using ARGOS.Domain.Aggregates.Organization.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ARGOS.Infrastructure.Persistence.Configurations;

public sealed class WorkflowConfiguration : IEntityTypeConfiguration<Workflow>
{
    public void Configure(EntityTypeBuilder<Workflow> b)
    {
        b.ToTable("Workflows");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasConversion(x => x.Value, x => WorkflowId.Create(x)).ValueGeneratedNever();
        b.Property(x => x.ProcedureId).HasConversion(x => x.Value, x => ProcedureId.From(x));
        b.Property(x => x.OrganizationId).HasConversion(x => x.Value, x => OrganizationId.From(x));
        b.Property(x => x.Code).HasMaxLength(100).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2000).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        b.HasIndex(x => new { x.OrganizationId, x.ProcedureId, x.Code }).IsUnique();
        b.HasOne<Procedure>().WithMany().HasForeignKey(x => x.ProcedureId).OnDelete(DeleteBehavior.Restrict);
        b.Ignore(x => x.DomainEvents);
        b.Ignore(x => x.HasDomainEvents);
        b.OwnsMany(x => x.Activities, a =>
        {
            a.ToTable("WorkflowActivities");
            a.WithOwner().HasForeignKey("WorkflowId");
            a.HasKey(x => x.Id);
            a.Property(x => x.Id).HasConversion(x => x.Value, x => WorkflowActivityId.Create(x)).ValueGeneratedNever();
            a.Property(x => x.Code).HasMaxLength(100).IsRequired();
            a.Property(x => x.Name).HasMaxLength(200).IsRequired();
            a.Property(x => x.Description).HasMaxLength(2000).IsRequired();
            a.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            a.HasIndex("WorkflowId", "Code").IsUnique();
        });
        b.OwnsMany(x => x.Transitions, t =>
        {
            t.ToTable("WorkflowTransitions");
            t.WithOwner().HasForeignKey("WorkflowId");
            t.HasKey(x => x.Id);
            t.Property(x => x.Id).HasConversion(x => x.Value, x => WorkflowTransitionId.Create(x)).ValueGeneratedNever();
            t.Property(x => x.SourceActivityId).HasConversion(x => x.Value, x => WorkflowActivityId.Create(x));
            t.Property(x => x.DestinationActivityId).HasConversion(x => x.Value, x => WorkflowActivityId.Create(x));
            t.Property(x => x.Name).HasMaxLength(200).IsRequired();
        });
        b.OwnsMany(x => x.Conditions, c =>
        {
            c.ToTable("WorkflowConditions");
            c.WithOwner().HasForeignKey("WorkflowId");
            c.HasKey(x => x.Id);
            c.Property(x => x.Id).HasConversion(x => x.Value, x => WorkflowConditionId.Create(x)).ValueGeneratedNever();
            c.Property(x => x.TransitionId).HasConversion(x => x.Value, x => WorkflowTransitionId.Create(x));
            c.Property(x => x.Expression).HasMaxLength(2000).IsRequired();
            c.HasIndex(x => new { x.TransitionId, x.EvaluationOrder }).IsUnique();
        });
        b.Navigation(x => x.Activities).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Navigation(x => x.Transitions).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Navigation(x => x.Conditions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
