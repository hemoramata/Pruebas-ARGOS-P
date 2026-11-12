using ARGOS.SharedKernel.Abstractions;

namespace ARGOS.Domain.Aggregates.Evidence.ValueObjects;

public sealed class EvidenceId : ValueObject
{
    public Guid Value { get; }

    private EvidenceId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("Evidence id cannot be empty.", nameof(value));

        Value = value;
    }

    public static EvidenceId New() => new(Guid.NewGuid());

    public static EvidenceId From(Guid value) => new(value);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
