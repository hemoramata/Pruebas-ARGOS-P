using ARGOS.Domain.Aggregates.Workflow.Conditions;

namespace ARGOS.Domain.Aggregates.Workflow.Conditions.Specifications;

/// <summary>
/// Defines the business rules for workflow conditions.
/// </summary>
public sealed class WorkflowConditionSpecification
{
    public void Validate(WorkflowCondition condition)
    {
        ArgumentNullException.ThrowIfNull(condition);

        if (condition.EvaluationOrder < 1)
            throw new InvalidOperationException(
                "EvaluationOrder must be greater than zero.");

        if (string.IsNullOrWhiteSpace(condition.Expression))
            throw new InvalidOperationException(
                "The condition expression cannot be empty.");
    }
}