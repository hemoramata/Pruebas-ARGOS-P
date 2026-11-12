using ARGOS.Domain.Aggregates.Organization.ValueObjects;
using ARGOS.Domain.Aggregates.Procedure.ValueObjects;
using ARGOS.Domain.Aggregates.Workflow.Enums;
using ARGOS.Domain.Aggregates.Workflow.Events;
using ARGOS.Domain.Aggregates.Workflow.ValueObjects;
using ARGOS.SharedKernel.Abstractions;

namespace ARGOS.Domain.Aggregates.Workflow;

/// <summary>
/// Represents the execution model associated with a procedure.
/// A workflow defines how a procedure is executed within an organization.
/// </summary>
public sealed class Workflow : AggregateRoot<WorkflowId>
{
    private readonly List<Activities.WorkflowActivity> _activities = [];
    private readonly List<Transitions.WorkflowTransition> _transitions = [];
    private readonly List<Conditions.WorkflowCondition> _conditions = [];
    public IReadOnlyCollection<Activities.WorkflowActivity> Activities => _activities.AsReadOnly();
    public IReadOnlyCollection<Transitions.WorkflowTransition> Transitions => _transitions.AsReadOnly();
    public IReadOnlyCollection<Conditions.WorkflowCondition> Conditions => _conditions.AsReadOnly();

    public void AddActivity(Activities.WorkflowActivity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);
        RequireDraft();
        if (_activities.Any(x => x.Id == activity.Id || x.Code == activity.Code))
            throw new InvalidOperationException("La actividad ya existe en el flujo.");
        _activities.Add(activity);
    }

    public void AddTransition(Transitions.WorkflowTransition transition)
    {
        ArgumentNullException.ThrowIfNull(transition);
        RequireDraft();
        if (_transitions.Any(x => x.Id == transition.Id) ||
            !_activities.Any(x => x.Id == transition.SourceActivityId) ||
            !_activities.Any(x => x.Id == transition.DestinationActivityId))
            throw new InvalidOperationException("La transición es duplicada o referencia actividades ajenas al flujo.");
        _transitions.Add(transition);
    }

    public void AddCondition(Conditions.WorkflowCondition condition)
    {
        ArgumentNullException.ThrowIfNull(condition);
        RequireDraft();
        if (!_transitions.Any(x => x.Id == condition.TransitionId) ||
            _conditions.Any(x => x.Id == condition.Id ||
                (x.TransitionId == condition.TransitionId && x.EvaluationOrder == condition.EvaluationOrder)))
            throw new InvalidOperationException("La condición tiene una transición inexistente o un orden duplicado.");
        _conditions.Add(condition);
    }

    private void RequireDraft()
    {
        if (Status != WorkflowStatus.Draft)
            throw new InvalidOperationException("Solo se puede configurar un flujo en borrador.");
    }
    private Workflow()
    {
    }

    private Workflow(
        WorkflowId id,
        ProcedureId procedureId,
        OrganizationId organizationId,
        string code,
        string name,
        string description)
        : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        ProcedureId = procedureId ?? throw new ArgumentNullException(nameof(procedureId));
        OrganizationId = organizationId ?? throw new ArgumentNullException(nameof(organizationId));

        Code = code.Trim();
        Name = name.Trim();
        Description = description.Trim();

        Status = WorkflowStatus.Draft;
    }

    public ProcedureId ProcedureId { get; private set; } = default!;

    public OrganizationId OrganizationId { get; private set; } = default!;

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public WorkflowStatus Status { get; private set; }

    public static Workflow Create(
        ProcedureId procedureId,
        OrganizationId organizationId,
        string code,
        string name,
        string description)
    {
        var workflow = new Workflow(
            WorkflowId.New(),
            procedureId,
            organizationId,
            code,
            name,
            description);

        workflow.RaiseDomainEvent(
            new WorkflowCreatedEvent(workflow.Id));

        return workflow;
    }

    public void Activate()
    {
        Status = WorkflowStatus.Active;
    }

    public void Deactivate()
    {
        Status = WorkflowStatus.Inactive;
    }

    public void Archive()
    {
        Status = WorkflowStatus.Archived;
    }
}
