using ARGOS.Domain.Aggregates.Organization.ValueObjects;
using ARGOS.SharedKernel.Abstractions;

namespace ARGOS.Domain.Aggregates.Organization.Events;

/// <summary>
/// Raised when a new organization is created.
/// </summary>
public sealed class OrganizationCreatedEvent : IDomainEvent
{
    public OrganizationCreatedEvent(OrganizationId organizationId)
    {
        OrganizationId = organizationId;
        OccurredOn = DateTime.UtcNow;
    }

    /// <summary>
    /// Organization identifier.
    /// </summary>
    public OrganizationId OrganizationId { get; }

    /// <inheritdoc />
    public DateTime OccurredOn { get; }
}