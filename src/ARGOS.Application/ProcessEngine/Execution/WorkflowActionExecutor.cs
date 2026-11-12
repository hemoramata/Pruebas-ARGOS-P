using ARGOS.Domain.Aggregates.Workflow.Actions;
using ARGOS.Application.ProcessEngine.Contracts;

namespace ARGOS.Application.ProcessEngine.Execution;

/// <summary>
/// Executes a workflow action within the current process execution.
/// </summary>
public sealed class WorkflowActionExecutor
{
    private readonly IActionCapability[] _capabilities;

    public WorkflowActionExecutor(IEnumerable<IActionCapability> capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        _capabilities = capabilities.ToArray();
    }

    public async Task ExecuteAsync(
        WorkflowAction action,
        ProcessExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(context);

        cancellationToken.ThrowIfCancellationRequested();
        if (context.CurrentAction is not null)
            throw new InvalidOperationException("Ya hay una acción en ejecución.");
        if (action.ActivityId != context.CurrentActivity.Id || action.ExecutionMoment != context.ExecutionMoment ||
            !context.Actions.Any(x => ReferenceEquals(x, action)))
            throw new InvalidOperationException("La acción no pertenece al plan actual.");

        // Ignore disabled actions.
        if (!action.IsActive)
        {
            context.SetItem("LastActionSkipped", action.Name);
            return;
        }

        var handlers = _capabilities.Where(x => x.ActionType == action.ActionType).ToArray();
        if (handlers.Length != 1)
            throw new InvalidOperationException("La acción requiere exactamente un ejecutor.");
        try
        {
            context.CurrentAction = action;
            await handlers[0].ExecuteAsync(context, cancellationToken);
        }
        finally { context.CurrentAction = null; }

        // These fields describe the last successfully completed action only.
        context.SetItem("LastActionId", action.Id);
        context.SetItem("LastActionName", action.Name);
        context.SetItem("LastActionType", action.ActionType);
        context.SetItem("LastExecutionMoment", action.ExecutionMoment);
        context.SetItem("LastExecutionOrder", action.ExecutionOrder);

        context.RemoveItem("LastActionSkipped");
    }
}
