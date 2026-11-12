using ARGOS.Domain.Aggregates.Workflow.Actions.ValueObjects;
using ARGOS.SharedKernel.Abstractions;

namespace ARGOS.Domain.Aggregates.Workflow.Actions.Events;

/// <summary>
/// Raised when a workflow action is created.
/// </summary>
public sealed class WorkflowActionCreatedEvent : IDomainEvent
{
    public WorkflowActionCreatedEvent(
        WorkflowActionId workflowActionId)
    {
        WorkflowActionId = workflowActionId;
        OccurredOn = DateTime.UtcNow;
    }

    public WorkflowActionId WorkflowActionId { get; }

    public DateTime OccurredOn { get; }
}