using ARGOS.SharedKernel.ValueObjects;

namespace ARGOS.Domain.Aggregates.Workflow.Conditions.ValueObjects;

/// <summary>
/// Unique identifier for a workflow condition.
/// </summary>
public sealed class WorkflowConditionId : EntityId
{
    private WorkflowConditionId(Guid value)
        : base(value)
    {
    }

    /// <summary>
    /// Creates a new unique workflow condition identifier.
    /// </summary>
    public static WorkflowConditionId New()
        => new(Guid.NewGuid());

    /// <summary>
    /// Creates a workflow condition identifier from an existing Guid.
    /// </summary>
    public static WorkflowConditionId Create(Guid value)
        => new(value);
}