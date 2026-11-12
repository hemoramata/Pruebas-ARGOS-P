using ARGOS.SharedKernel.Abstractions;

namespace ARGOS.Domain.Aggregates.Procedure.ValueObjects;

/// <summary>
/// Stable identity shared by every version of the same institutional procedure.
/// </summary>
public sealed class ProcedureDefinitionId : ValueObject
{
    public Guid Value { get; }

    private ProcedureDefinitionId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("Procedure definition id cannot be empty.", nameof(value));

        Value = value;
    }

    public static ProcedureDefinitionId New() => new(Guid.NewGuid());

    public static ProcedureDefinitionId From(Guid value) => new(value);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
