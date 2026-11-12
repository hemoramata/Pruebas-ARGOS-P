using ARGOS.SharedKernel.Abstractions;

namespace ARGOS.Domain.Aggregates.Workflow.Actions.ValueObjects;

/// <summary>
/// Represents the configuration required to execute
/// a workflow action.
/// </summary>
public sealed class WorkflowActionConfiguration : ValueObject
{
    private readonly List<WorkflowActionParameter> _parameters = new();

    private WorkflowActionConfiguration()
    {
    }

    private WorkflowActionConfiguration(
        IEnumerable<WorkflowActionParameter> parameters)
    {
        _parameters.AddRange(parameters);
    }

    public IReadOnlyCollection<WorkflowActionParameter> Parameters
        => _parameters.AsReadOnly();

    public static WorkflowActionConfiguration Create(
        IEnumerable<WorkflowActionParameter> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        return new WorkflowActionConfiguration(parameters);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        return _parameters;
    }
}