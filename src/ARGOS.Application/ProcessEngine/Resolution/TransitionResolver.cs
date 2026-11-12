using ARGOS.Application.ProcessEngine.Contracts;
using ARGOS.Application.ProcessEngine.Execution;
using ARGOS.Domain.Aggregates.Workflow.Activities;

namespace ARGOS.Application.ProcessEngine.Resolution;

/// <summary>
/// Default implementation responsible for resolving
/// the next activity in a workflow.
/// </summary>
public sealed class TransitionResolver : ITransitionResolver
{
    private readonly IConditionEvaluator _conditionEvaluator;

    public TransitionResolver(
        IConditionEvaluator conditionEvaluator)
    {
        _conditionEvaluator = conditionEvaluator
            ?? throw new ArgumentNullException(nameof(conditionEvaluator));
    }

    public async Task<WorkflowActivity?> ResolveAsync(
        ProcessExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        cancellationToken.ThrowIfCancellationRequested();
        if (!context.RoutingConfigured)
            throw new InvalidOperationException("Debe configurar las rutas del flujo antes de resolver una transición.");
        if (!context.Activities.Any(x => x.Id == context.CurrentActivity.Id))
            throw new InvalidOperationException("La actividad actual no pertenece a las rutas configuradas.");

        WorkflowActivity? destination = null;
        foreach (var transition in context.Transitions.Where(x => x.SourceActivityId == context.CurrentActivity.Id))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var allowed = true;
            foreach (var condition in context.Conditions.Where(x => x.TransitionId == transition.Id).OrderBy(x => x.EvaluationOrder))
            {
                if (!await _conditionEvaluator.EvaluateAsync(condition, context, cancellationToken))
                {
                    allowed = false;
                    break;
                }
            }
            if (!allowed) continue;
            if (destination is not null)
                throw new InvalidOperationException("Más de una transición cumple las condiciones; el flujo requiere una ruta inequívoca.");
            destination = context.Activities.Single(x => x.Id == transition.DestinationActivityId);
        }
        return destination;
    }
}
