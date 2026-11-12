using ARGOS.Domain.Aggregates.OrganizationUnit.Enums;
using ARGOS.Domain.Aggregates.OrganizationUnit.Events;
using ARGOS.Domain.Aggregates.OrganizationUnit.ValueObjects;
using ARGOS.SharedKernel.Abstractions;

namespace ARGOS.Domain.Aggregates.OrganizationUnit;

/// <summary>
/// Represents an organizational unit within an institution.
/// </summary>
public sealed class OrganizationUnit : AggregateRoot<OrganizationUnitId>
{
    /// <summary>
    /// Unit name.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Internal code.
    /// </summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>
    /// Optional description.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Current status.
    /// </summary>
    public OrganizationUnitStatus Status { get; private set; }

    private OrganizationUnit()
    {
    }

    private OrganizationUnit(
        OrganizationUnitId id,
        string name,
        string code,
        string? description)
        : base(id)
    {
        Name = name.Trim();
        Code = code.Trim().ToUpperInvariant();
        Description = description?.Trim();
        Status = OrganizationUnitStatus.Active;

        AddDomainEvent(new OrganizationUnitCreatedEvent(Id));
    }

    public static OrganizationUnit Create(
        string name,
        string code,
        string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Organization unit name is required.", nameof(name));

        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Organization unit code is required.", nameof(code));

        return new OrganizationUnit(
            OrganizationUnitId.New(),
            name,
            code,
            description);
    }

    public void Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("Organization unit name is required.", nameof(newName));

        Name = newName.Trim();
    }

    public void ChangeCode(string newCode)
    {
        if (string.IsNullOrWhiteSpace(newCode))
            throw new ArgumentException("Organization unit code is required.", nameof(newCode));

        Code = newCode.Trim().ToUpperInvariant();
    }

    public void ChangeDescription(string? description)
    {
        Description = description?.Trim();
    }

    public void Activate()
    {
        Status = OrganizationUnitStatus.Active;
    }

    public void Deactivate()
    {
        Status = OrganizationUnitStatus.Inactive;
    }
}