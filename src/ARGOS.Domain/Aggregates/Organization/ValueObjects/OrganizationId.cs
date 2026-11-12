using ARGOS.SharedKernel.Abstractions;

namespace ARGOS.Domain.Aggregates.Organization.ValueObjects;

/// <summary>
/// Represents the unique identifier of an Organization.
/// </summary>
public sealed class OrganizationId : ValueObject
{
    public Guid Value { get; }

    private OrganizationId(Guid value)
    {
        Value = value;
    }

    /// <summary>
    /// Creates a new Organization identifier.
    /// </summary>
    public static OrganizationId New()
        => new(Guid.NewGuid());

    /// <summary>
    /// Creates an OrganizationId from an existing Guid.
    /// </summary>
    public static OrganizationId From(Guid value)
        => new(value);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString()
        => Value.ToString();

    public static implicit operator Guid(OrganizationId id)
        => id.Value;
}