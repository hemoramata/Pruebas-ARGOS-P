using ARGOS.SharedKernel.ValueObjects;

namespace ARGOS.Domain.Aggregates.Workflow.Actions.ValueObjects;

/// <summary>
/// Unique identifier for a workflow action.
/// </summary>
public sealed class WorkflowActionId : EntityId
{
    private WorkflowActionId(Guid value)
        : base(value)
    {
    }

    /// <summary>
    /// Creates a new workflow action identifier.
    /// </summary>
    public static WorkflowActionId New()
        => new(Guid.NewGuid());

    /// <summary>
    /// Creates a workflow action identifier from an existing Guid.
    /// </summary>
    public static WorkflowActionId Create(Guid value)
        => new(value);
}