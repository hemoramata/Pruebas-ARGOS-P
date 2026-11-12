using ARGOS.SharedKernel.Abstractions;

namespace ARGOS.Domain.Aggregates.OrganizationSetup.ValueObjects;

public sealed class OrganizationSetupId : ValueObject
{
    private OrganizationSetupId(Guid value)
    {
        Value = value;
    }

    public Guid Value { get; }

    public static OrganizationSetupId New() => new(Guid.NewGuid());

    public static OrganizationSetupId From(Guid value) => new(value);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
