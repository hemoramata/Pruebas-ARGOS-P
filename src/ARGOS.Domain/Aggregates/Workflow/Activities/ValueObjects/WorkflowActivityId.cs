using ARGOS.SharedKernel.ValueObjects;

namespace ARGOS.Domain.Aggregates.Workflow.Activities.ValueObjects;

/// <summary>
/// Unique identifier for a workflow activity.
/// </summary>
public sealed class WorkflowActivityId : EntityId
{
    private WorkflowActivityId(Guid value)
        : base(value)
    {
    }

    public static WorkflowActivityId New()
        => new(Guid.NewGuid());

    public static WorkflowActivityId Create(Guid value)
        => new(value);
}