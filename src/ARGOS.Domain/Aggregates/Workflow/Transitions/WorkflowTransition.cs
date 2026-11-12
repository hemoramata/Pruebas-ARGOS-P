using ARGOS.Domain.Aggregates.Workflow.Activities.ValueObjects;
using ARGOS.Domain.Aggregates.Workflow.Transitions.ValueObjects;
using ARGOS.SharedKernel.Abstractions;

namespace ARGOS.Domain.Aggregates.Workflow.Transitions;

/// <summary>
/// Represents a transition between two workflow activities.
/// A transition defines the possible path from one activity to another.
/// </summary>
public sealed class WorkflowTransition : Entity<WorkflowTransitionId>
{
    private WorkflowTransition()
    {
    }

    private WorkflowTransition(
        WorkflowTransitionId id,
        WorkflowActivityId sourceActivityId,
        WorkflowActivityId destinationActivityId,
        string name)
        : base(id)
    {
        ArgumentNullException.ThrowIfNull(sourceActivityId);
        ArgumentNullException.ThrowIfNull(destinationActivityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        SourceActivityId = sourceActivityId;
        DestinationActivityId = destinationActivityId;
        Name = name.Trim();
    }

    /// <summary>
    /// Activity where the transition starts.
    /// </summary>
    public WorkflowActivityId SourceActivityId { get; private set; } = default!;

    /// <summary>
    /// Activity where the transition ends.
    /// </summary>
    public WorkflowActivityId DestinationActivityId { get; private set; } = default!;

    /// <summary>
    /// Friendly transition name.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    public static WorkflowTransition Create(
        WorkflowActivityId sourceActivityId,
        WorkflowActivityId destinationActivityId,
        string name)
    {
        return new WorkflowTransition(
            WorkflowTransitionId.New(),
            sourceActivityId,
            destinationActivityId,
            name);
    }
}