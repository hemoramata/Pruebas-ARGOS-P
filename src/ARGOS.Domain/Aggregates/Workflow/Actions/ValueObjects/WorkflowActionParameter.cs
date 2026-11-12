using ARGOS.SharedKernel.Abstractions;

namespace ARGOS.Domain.Aggregates.Workflow.Actions.ValueObjects;

/// <summary>
/// Represents a configuration parameter for a workflow action.
/// </summary>
public sealed class WorkflowActionParameter : ValueObject
{
    private WorkflowActionParameter()
    {
    }

    private WorkflowActionParameter(
        string name,
        string value,
        string dataType,
        bool required,
        string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataType);

        Name = name.Trim();
        Value = value ?? string.Empty;
        DataType = dataType.Trim();
        Required = required;
        Description = description ?? string.Empty;
    }

    public string Name { get; } = string.Empty;

    public string Value { get; } = string.Empty;

    public string DataType { get; } = string.Empty;

    public bool Required { get; }

    public string Description { get; } = string.Empty;

    public static WorkflowActionParameter Create(
        string name,
        string value,
        string dataType,
        bool required,
        string description)
        => new(name, value, dataType, required, description);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Name;
        yield return Value;
        yield return DataType;
        yield return Required;
        yield return Description;
    }
}
