using ARGOS.Domain.Aggregates.Workflow.Conditions.ValueObjects;
using ARGOS.Domain.Aggregates.Workflow.Transitions.ValueObjects;
using ARGOS.SharedKernel.Abstractions;

namespace ARGOS.Domain.Aggregates.Workflow.Conditions;

/// <summary>
/// Represents a condition that must be evaluated before a workflow
/// transition can be executed.
/// </summary>
public sealed class WorkflowCondition : Entity<WorkflowConditionId>
{
    private WorkflowCondition()
    {
    }

    private WorkflowCondition(
        WorkflowConditionId id,
        WorkflowTransitionId transitionId,
        string expression,
        int evaluationOrder)
        : base(id)
    {
        ArgumentNullException.ThrowIfNull(transitionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);

        if (evaluationOrder < 1)
            throw new ArgumentOutOfRangeException(nameof(evaluationOrder));

        TransitionId = transitionId;
        Expression = expression.Trim();
        EvaluationOrder = evaluationOrder;
    }

    /// <summary>
    /// Transition that owns this condition.
    /// </summary>
    public WorkflowTransitionId TransitionId { get; private set; } = default!;

    /// <summary>
    /// Expression that will be evaluated.
    /// Example:
    /// DocumentComplete == true
    /// </summary>
    public string Expression { get; private set; } = string.Empty;

    /// <summary>
    /// Evaluation sequence when multiple conditions exist.
    /// </summary>
    public int EvaluationOrder { get; private set; }

    public static WorkflowCondition Create(
        WorkflowTransitionId transitionId,
        string expression,
        int evaluationOrder)
    {
        return new WorkflowCondition(
            WorkflowConditionId.New(),
            transitionId,
            expression,
            evaluationOrder);
    }
}