using ARGOS.SharedKernel.Abstractions;
using ARGOS.SharedKernel.ValueObjects;

namespace ARGOS.Domain.Aggregates.Workflow.ValueObjects;

/// <summary>
/// Unique identifier for a Workflow.
/// </summary>
public sealed class WorkflowId : EntityId
{
    private WorkflowId(Guid value)
        : base(value)
    {
    }

    public static WorkflowId New()
        => new(Guid.NewGuid());

    public static WorkflowId Create(Guid value)
        => new(value);
}