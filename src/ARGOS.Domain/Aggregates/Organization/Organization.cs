using ARGOS.Domain.Aggregates.Organization.Enums;
using ARGOS.Domain.Aggregates.Organization.Events;
using ARGOS.Domain.Aggregates.Organization.ValueObjects;
using ARGOS.SharedKernel.Abstractions;

namespace ARGOS.Domain.Aggregates.Organization;

/// <summary>
/// Represents an organization (institution) within ARGOS.
/// Each ARGOS installation is currently intended for a single organization,
/// but the domain keeps this aggregate to support future multi-organization scenarios.
/// </summary>
public sealed class Organization : AggregateRoot<OrganizationId>
{
    /// <summary>
    /// Official organization name.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Internal organization code.
    /// </summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>
    /// Optional description.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Current organization status.
    /// </summary>
    public OrganizationStatus Status { get; private set; }

    private Organization()
    {
    }

    private Organization(
        OrganizationId id,
        string name,
        string code,
        string? description)
        : base(id)
    {
        Name = name.Trim();
        Code = code.Trim().ToUpperInvariant();
        Description = description?.Trim();
        Status = OrganizationStatus.Active;

        AddDomainEvent(new OrganizationCreatedEvent(Id));
    }

    /// <summary>
    /// Factory method for creating organizations.
    /// </summary>
    public static Organization Create(
        string name,
        string code,
        string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Organization name is required.", nameof(name));

        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Organization code is required.", nameof(code));

        return new Organization(
            OrganizationId.New(),
            name,
            code,
            description);
    }

    public void Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("Organization name is required.", nameof(newName));

        Name = newName.Trim();
    }

    public void ChangeCode(string newCode)
    {
        if (string.IsNullOrWhiteSpace(newCode))
            throw new ArgumentException("Organization code is required.", nameof(newCode));

        Code = newCode.Trim().ToUpperInvariant();
    }

    public void ChangeDescription(string? description)
    {
        Description = description?.Trim();
    }

    public void Activate()
    {
        Status = OrganizationStatus.Active;
    }

    public void Deactivate()
    {
        Status = OrganizationStatus.Inactive;
    }
}