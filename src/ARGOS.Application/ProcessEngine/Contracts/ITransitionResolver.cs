using ARGOS.Application.ProcessEngine.Execution;
using ARGOS.Domain.Aggregates.Workflow.Activities;

namespace ARGOS.Application.ProcessEngine.Contracts;

/// <summary>
/// Defines the contract responsible for resolving
/// the next workflow activity.
/// </summary>
public interface ITransitionResolver
{
    Task<WorkflowActivity?> ResolveAsync(
        ProcessExecutionContext context,
        CancellationToken cancellationToken = default);
}