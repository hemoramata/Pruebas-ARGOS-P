using ARGOS.SharedKernel.ValueObjects;

namespace ARGOS.Domain.Aggregates.Workflow.Assignments.ValueObjects;

/// <summary>
/// Unique identifier for a workflow assignment.
/// </summary>
public sealed class WorkflowAssignmentId : EntityId
{
    private WorkflowAssignmentId(Guid value)
        : base(value)
    {
    }

    public static WorkflowAssignmentId New()
        => new(Guid.NewGuid());

    public static WorkflowAssignmentId Create(Guid value)
        => new(value);
}