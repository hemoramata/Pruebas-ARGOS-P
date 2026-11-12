using ARGOS.Domain.Aggregates.Workflow.Conditions.ValueObjects;
using ARGOS.SharedKernel.Abstractions;

namespace ARGOS.Domain.Aggregates.Workflow.Conditions.Events;

/// <summary>
/// Raised when a new workflow condition is created.
/// </summary>
public sealed class WorkflowConditionCreatedEvent : IDomainEvent
{
    public WorkflowConditionCreatedEvent(WorkflowConditionId workflowConditionId)
    {
        WorkflowConditionId = workflowConditionId;
        OccurredOn = DateTime.UtcNow;
    }

    /// <summary>
    /// Identifier of the created workflow condition.
    /// </summary>
    public WorkflowConditionId WorkflowConditionId { get; }

    /// <summary>
    /// Date and time when the event occurred.
    /// </summary>
    public DateTime OccurredOn { get; }
}