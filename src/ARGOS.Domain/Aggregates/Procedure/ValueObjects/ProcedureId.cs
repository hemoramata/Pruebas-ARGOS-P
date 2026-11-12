using ARGOS.SharedKernel.Abstractions;

namespace ARGOS.Domain.Aggregates.Procedure.ValueObjects;

/// <summary>
/// Represents the unique identifier of a Procedure.
/// </summary>
public sealed class ProcedureId : ValueObject
{
    public Guid Value { get; }

    private ProcedureId(Guid value)
    {
        Value = value;
    }

    /// <summary>
    /// Creates a new Procedure identifier.
    /// </summary>
    public static ProcedureId New()
        => new(Guid.NewGuid());

    /// <summary>
    /// Creates a ProcedureId from an existing Guid.
    /// </summary>
    public static ProcedureId From(Guid value)
        => new(value);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString()
        => Value.ToString();

    public static implicit operator Guid(ProcedureId id)
        => id.Value;
}