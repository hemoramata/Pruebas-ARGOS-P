using ARGOS.Domain.Aggregates.Workflow.Assignments.ValueObjects;
using ARGOS.SharedKernel.Abstractions;

namespace ARGOS.Domain.Aggregates.Workflow.Assignments.Events;

/// <summary>
/// Raised when a workflow assignment is created.
/// </summary>
public sealed class WorkflowAssignmentCreatedEvent : IDomainEvent
{
    public WorkflowAssignmentCreatedEvent(
        WorkflowAssignmentId workflowAssignmentId)
    {
        WorkflowAssignmentId = workflowAssignmentId;
        OccurredOn = DateTime.UtcNow;
    }

    /// <summary>
    /// Identifier of the created workflow assignment.
    /// </summary>
    public WorkflowAssignmentId WorkflowAssignmentId { get; }

    /// <summary>
    /// Date and time when the event occurred.
    /// </summary>
    public DateTime OccurredOn { get; }
}