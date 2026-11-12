using ARGOS.SharedKernel.Abstractions;

namespace ARGOS.Domain.Aggregates.OrganizationUnit.ValueObjects;

public sealed class OrganizationUnitId : ValueObject
{
    public Guid Value { get; }

    private OrganizationUnitId(Guid value)
    {
        Value = value;
    }

    public static OrganizationUnitId New()
        => new(Guid.NewGuid());

    public static OrganizationUnitId From(Guid value)
        => new(value);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString()
        => Value.ToString();
}