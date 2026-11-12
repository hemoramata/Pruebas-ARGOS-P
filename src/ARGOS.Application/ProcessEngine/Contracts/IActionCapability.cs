using ARGOS.Application.ProcessEngine.Execution;
using ARGOS.Domain.Aggregates.Workflow.Actions.Enums;

namespace ARGOS.Application.ProcessEngine.Contracts;

public interface IActionCapability
{
    WorkflowActionType ActionType { get; }

    Task ExecuteAsync(
        ProcessExecutionContext context,
        CancellationToken cancellationToken = default);
}