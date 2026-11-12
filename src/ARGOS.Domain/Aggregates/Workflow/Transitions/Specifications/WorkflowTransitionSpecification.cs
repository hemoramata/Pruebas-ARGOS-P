using ARGOS.Domain.Aggregates.Workflow.Transitions;

namespace ARGOS.Domain.Aggregates.Workflow.Transitions.Specifications;

/// <summary>
/// Defines the business rules that every workflow transition must satisfy.
/// </summary>
public sealed class WorkflowTransitionSpecification
{
    public void Validate(WorkflowTransition transition)
    {
        ArgumentNullException.ThrowIfNull(transition);

        if (transition.SourceActivityId == transition.DestinationActivityId)
            throw new InvalidOperationException(
                "A workflow transition cannot point to the same activity.");
    }
}