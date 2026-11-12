using ARGOS.Domain.Aggregates.OrganizationUnit.ValueObjects;
using ARGOS.SharedKernel.Abstractions;

namespace ARGOS.Domain.Aggregates.OrganizationUnit.Events;

/// <summary>
/// Domain event raised when an Organization Unit is created.
/// </summary>
public sealed class OrganizationUnitCreatedEvent : IDomainEvent
{
    public OrganizationUnitCreatedEvent(OrganizationUnitId organizationUnitId)
    {
        OrganizationUnitId = organizationUnitId;
        OccurredOn = DateTime.UtcNow;
    }

    /// <summary>
    /// Organization Unit identifier.
    /// </summary>
    public OrganizationUnitId OrganizationUnitId { get; }

    /// <inheritdoc />
    public DateTime OccurredOn { get; }
}