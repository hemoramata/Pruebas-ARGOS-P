using ARGOS.Application.ProcessEngine.Execution;
using ARGOS.Domain.Aggregates.Workflow.Conditions;

namespace ARGOS.Application.ProcessEngine.Contracts;

/// <summary>
/// Evaluates a workflow condition within the current execution context.
/// </summary>
public interface IConditionEvaluator
{
    Task<bool> EvaluateAsync(
        WorkflowCondition condition,
        ProcessExecutionContext context,
        CancellationToken cancellationToken = default);
}