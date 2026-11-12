using ARGOS.Domain.Aggregates.Workflow.Activities.ValueObjects;
using ARGOS.SharedKernel.Abstractions;

namespace ARGOS.Domain.Aggregates.Workflow.Activities.Events;

/// <summary>
/// Raised when a new workflow activity is created.
/// </summary>
public sealed class WorkflowActivityCreatedEvent : IDomainEvent
{
    public WorkflowActivityCreatedEvent(WorkflowActivityId workflowActivityId)
    {
        WorkflowActivityId = workflowActivityId;
        OccurredOn = DateTime.UtcNow;
    }

    /// <summary>
    /// Identifier of the created workflow activity.
    /// </summary>
    public WorkflowActivityId WorkflowActivityId { get; }

    /// <summary>
    /// Date and time when the event occurred.
    /// </summary>
    public DateTime OccurredOn { get; }
}