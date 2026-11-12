using ARGOS.SharedKernel.ValueObjects;

namespace ARGOS.Domain.Aggregates.Workflow.Transitions.ValueObjects;

/// <summary>
/// Unique identifier for a workflow transition.
/// </summary>
public sealed class WorkflowTransitionId : EntityId
{
    private WorkflowTransitionId(Guid value)
        : base(value)
    {
    }

    public static WorkflowTransitionId New()
        => new(Guid.NewGuid());

    public static WorkflowTransitionId Create(Guid value)
        => new(value);
}