using ARGOS.Domain.Aggregates.Workflow.ValueObjects;

namespace ARGOS.Infrastructure.Persistence;

public sealed class CaseWorkflowRun
{
    public Guid Id { get; set; }
    public Guid CaseFileId { get; set; }
    public Guid OrganizationId { get; set; }
    public WorkflowId WorkflowId { get; set; } = null!;
    public Guid CurrentActivityId { get; set; }
    public long Revision { get; set; }
}

public sealed class CaseWorkflowStep
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public long Revision { get; set; }
    public Guid? FromActivityId { get; set; }
    public Guid ToActivityId { get; set; }
    public Guid AuditEntryId { get; set; }
}
