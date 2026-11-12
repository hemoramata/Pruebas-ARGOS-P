using ARGOS.Domain.Aggregates.Workflow;
using ARGOS.Domain.Aggregates.Workflow.Activities;
using ARGOS.Domain.Aggregates.Workflow.Transitions;
using ARGOS.Domain.Aggregates.Workflow.Conditions;
using ARGOS.Domain.Aggregates.Workflow.Actions;
using ARGOS.Domain.Aggregates.Workflow.Actions.Enums;

namespace ARGOS.Application.ProcessEngine.Execution;

/// <summary>
/// Represents the execution context of a business process.
/// Contains the complete execution state required by the Process Engine.
/// </summary>
public sealed class ProcessExecutionContext
{
    private readonly Dictionary<string, object> _items = new();
    public IReadOnlyList<WorkflowAction> Actions { get; private set; } = Array.Empty<WorkflowAction>();
    public WorkflowExecutionMoment? ExecutionMoment { get; private set; }
    public WorkflowAction? CurrentAction { get; internal set; }

    public void ConfigureActions(IEnumerable<WorkflowAction> actions, WorkflowExecutionMoment moment)
    {
        ArgumentNullException.ThrowIfNull(actions);
        if (!Enum.IsDefined(moment)) throw new ArgumentOutOfRangeException(nameof(moment));
        if (!RoutingConfigured) throw new InvalidOperationException("Configure las rutas antes de las acciones.");
        var snapshot = actions.ToArray();
        if (snapshot.Any(x => x is null) || snapshot.Select(x => x.Id).Distinct().Count() != snapshot.Length)
            throw new ArgumentException("Las acciones contienen elementos nulos o duplicados.");
        if (snapshot.Any(x => !Activities.Any(a => a.Id == x.ActivityId)))
            throw new ArgumentException("Una acción pertenece a una actividad ajena al flujo.");
        if (snapshot.GroupBy(x => (x.ActivityId, x.ExecutionMoment, x.ExecutionOrder)).Any(x => x.Count() > 1))
            throw new ArgumentException("El orden de ejecución debe ser único por actividad y momento.");
        Actions = Array.AsReadOnly(snapshot);
        ExecutionMoment = moment;
    }

    public IReadOnlyList<WorkflowActivity> Activities { get; private set; } = Array.Empty<WorkflowActivity>();
    public IReadOnlyList<WorkflowTransition> Transitions { get; private set; } = Array.Empty<WorkflowTransition>();
    public IReadOnlyList<WorkflowCondition> Conditions { get; private set; } = Array.Empty<WorkflowCondition>();
    public bool RoutingConfigured { get; private set; }

    public void ConfigureRouting(
        IEnumerable<WorkflowActivity> activities,
        IEnumerable<WorkflowTransition> transitions,
        IEnumerable<WorkflowCondition> conditions)
    {
        ArgumentNullException.ThrowIfNull(activities);
        ArgumentNullException.ThrowIfNull(transitions);
        ArgumentNullException.ThrowIfNull(conditions);
        var nodes = activities.ToArray();
        var edges = transitions.ToArray();
        var rules = conditions.ToArray();
        if (nodes.Any(x => x is null) || edges.Any(x => x is null) || rules.Any(x => x is null))
            throw new ArgumentException("La configuración contiene elementos nulos.");
        if (nodes.Select(x => x.Id).Distinct().Count() != nodes.Length ||
            edges.Select(x => x.Id).Distinct().Count() != edges.Length ||
            rules.Select(x => x.Id).Distinct().Count() != rules.Length)
            throw new ArgumentException("La configuración contiene identificadores duplicados.");
        var nodeIds = nodes.Select(x => x.Id).ToHashSet();
        var edgeIds = edges.Select(x => x.Id).ToHashSet();
        if (!nodeIds.Contains(CurrentActivity.Id) ||
            edges.Any(x => !nodeIds.Contains(x.SourceActivityId) || !nodeIds.Contains(x.DestinationActivityId)) ||
            rules.Any(x => !edgeIds.Contains(x.TransitionId)))
            throw new ArgumentException("La configuración contiene referencias a actividades o transiciones inexistentes.");
        if (rules.GroupBy(x => (x.TransitionId, x.EvaluationOrder)).Any(x => x.Count() > 1))
            throw new ArgumentException("El orden de las condiciones debe ser único dentro de cada transición.");
        Activities = Array.AsReadOnly(nodes);
        Transitions = Array.AsReadOnly(edges);
        Conditions = Array.AsReadOnly(rules);
        Actions = Array.Empty<WorkflowAction>();
        ExecutionMoment = null;
        RoutingConfigured = true;
    }

    public ProcessExecutionContext(
        Workflow workflow,
        WorkflowActivity currentActivity,
        Guid processInstanceId)
    {
        Workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
        CurrentActivity = currentActivity ?? throw new ArgumentNullException(nameof(currentActivity));

        ProcessInstanceId = processInstanceId;
        CorrelationId = Guid.NewGuid();
        ExecutionDate = DateTime.UtcNow;
    }

    public Workflow Workflow { get; }

    public WorkflowActivity CurrentActivity { get; private set; }

    public Guid ProcessInstanceId { get; }

    public Guid CorrelationId { get; }

    public DateTime ExecutionDate { get; }

    public IReadOnlyDictionary<string, object> Items => _items;

    public void ChangeActivity(WorkflowActivity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);

        CurrentActivity = activity;
    }

    public void SetItem(string key, object value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        _items[key] = value;
    }

    public T? GetItem<T>(string key)
    {
        if (_items.TryGetValue(key, out var value))
            return (T?)value;

        return default;
    }

    public bool ContainsItem(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return _items.ContainsKey(key);
    }

    public bool RemoveItem(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return _items.Remove(key);
    }
}
