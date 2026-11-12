using ARGOS.Application.ProcessEngine.Contracts;
using ARGOS.Application.ProcessEngine.Execution;
using ARGOS.Application.ProcessEngine.Resolution;

namespace ARGOS.Application.ProcessEngine;

/// <summary>
/// Default implementation of the ARGOS Process Engine.
/// Responsible for orchestrating the execution of business processes.
/// </summary>
public sealed class ProcessEngine : IProcessEngine
{
    private readonly IActionDispatcher _actionDispatcher;
    private readonly ITransitionResolver _transitionResolver;

    public ProcessEngine(
        IActionDispatcher actionDispatcher,
        ITransitionResolver transitionResolver)
    {
        _actionDispatcher = actionDispatcher
            ?? throw new ArgumentNullException(nameof(actionDispatcher));

        _transitionResolver = transitionResolver
            ?? throw new ArgumentNullException(nameof(transitionResolver));
    }

    public async Task ExecuteAsync(
        ProcessExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        await _actionDispatcher.DispatchAsync(
            context,
            cancellationToken);

        var nextActivity =
            await _transitionResolver.ResolveAsync(
                context,
                cancellationToken);

        if (nextActivity is not null)
        {
            context.ChangeActivity(nextActivity);
        }
    }
}