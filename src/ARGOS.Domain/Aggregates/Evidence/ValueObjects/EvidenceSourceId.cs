using ARGOS.SharedKernel.Abstractions;

namespace ARGOS.Domain.Aggregates.Evidence.ValueObjects;

public sealed class EvidenceSourceId : ValueObject
{
    public Guid Value { get; }

    private EvidenceSourceId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("Evidence source id cannot be empty.", nameof(value));

        Value = value;
    }

    public static EvidenceSourceId New() => new(Guid.NewGuid());

    public static EvidenceSourceId From(Guid value) => new(value);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}
