using ARGOS.Domain.Aggregates.CaseFile;
using ARGOS.Domain.Aggregates.Workflow;
using ARGOS.Domain.Aggregates.Workflow.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ARGOS.Infrastructure.Persistence.Configurations;

public sealed class CaseWorkflowRunConfiguration : IEntityTypeConfiguration<CaseWorkflowRun>
{
    public void Configure(EntityTypeBuilder<CaseWorkflowRun> b)
    {
        b.ToTable("CaseWorkflowRuns");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.WorkflowId).HasConversion(x => x.Value, x => WorkflowId.Create(x));
        b.Property(x => x.Revision).IsConcurrencyToken();
        b.HasIndex(x => new { x.CaseFileId, x.WorkflowId }).IsUnique();
        b.HasOne<CaseFile>().WithMany().HasForeignKey(x => x.CaseFileId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Workflow>().WithMany().HasForeignKey(x => x.WorkflowId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CaseWorkflowStepConfiguration : IEntityTypeConfiguration<CaseWorkflowStep>
{
    public void Configure(EntityTypeBuilder<CaseWorkflowStep> b)
    {
        b.ToTable("CaseWorkflowSteps");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.HasIndex(x => new { x.RunId, x.Revision }).IsUnique();
        b.HasOne<CaseWorkflowRun>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AuditEntry>().WithMany().HasForeignKey(x => x.AuditEntryId).OnDelete(DeleteBehavior.Restrict);
    }
}
