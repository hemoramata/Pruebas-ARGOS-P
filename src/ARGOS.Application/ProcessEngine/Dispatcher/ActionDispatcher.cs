using ARGOS.Application.ProcessEngine.Contracts;
using ARGOS.Application.ProcessEngine.Execution;

namespace ARGOS.Application.ProcessEngine.Dispatcher;

/// <summary>
/// Default dispatcher responsible for invoking workflow action capabilities.
/// </summary>
public sealed class ActionDispatcher : IActionDispatcher
{
    private readonly IEnumerable<IActionCapability> _capabilities;

    public ActionDispatcher(
        IEnumerable<IActionCapability> capabilities)
    {
        _capabilities = capabilities
            ?? throw new ArgumentNullException(nameof(capabilities));
    }

    public async Task DispatchAsync(
        ProcessExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (context.ExecutionMoment is null)
            throw new InvalidOperationException("Debe configurar las acciones y su momento de ejecución.");
        if (context.CurrentAction is not null)
            throw new InvalidOperationException("Ya hay una acción en ejecución en este contexto.");

        var capabilities = _capabilities.ToArray();
        var actions = context.Actions
            .Where(x => x.IsActive && x.ActivityId == context.CurrentActivity.Id && x.ExecutionMoment == context.ExecutionMoment)
            .OrderBy(x => x.ExecutionOrder).ToArray();
        // Validate the complete plan before executing any capability.
        var plan = actions.Select(action =>
        {
            var matches = capabilities.Where(x => x.ActionType == action.ActionType).ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException($"La acción '{action.Name}' requiere una única capacidad para {action.ActionType}.");
            return (Action: action, Capability: matches[0]);
        }).ToArray();

        var executor = new WorkflowActionExecutor(capabilities);
        foreach (var item in plan)
        {
            await executor.ExecuteAsync(item.Action, context, cancellationToken);
        }
    }
}
