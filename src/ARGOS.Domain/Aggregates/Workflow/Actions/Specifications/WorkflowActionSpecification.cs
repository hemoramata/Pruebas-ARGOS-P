using ARGOS.Domain.Aggregates.Workflow.Actions;

namespace ARGOS.Domain.Aggregates.Workflow.Actions.Specifications;

/// <summary>
/// Defines the business rules for workflow actions.
/// </summary>
public sealed class WorkflowActionSpecification
{
    public void Validate(WorkflowAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (action.ExecutionOrder < 1)
            throw new InvalidOperationException(
                "ExecutionOrder must be greater than zero.");

        if (action.Configuration is null)
            throw new InvalidOperationException(
                "A workflow action must define its configuration.");
    }
}