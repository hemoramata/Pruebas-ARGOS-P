using ARGOS.Domain.Aggregates.Workflow.ValueObjects;
using ARGOS.SharedKernel.Abstractions;

namespace ARGOS.Domain.Aggregates.Workflow.Events;

/// <summary>
/// Raised when a new workflow is created.
/// </summary>
public sealed class WorkflowCreatedEvent : IDomainEvent
{
    public WorkflowCreatedEvent(WorkflowId workflowId)
    {
        WorkflowId = workflowId;
        OccurredOn = DateTime.UtcNow;
    }

    public WorkflowId WorkflowId { get; }

    public DateTime OccurredOn { get; }
}