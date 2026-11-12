using ARGOS.Domain.Aggregates.Organization.ValueObjects;
using ARGOS.Domain.Aggregates.OrganizationSetup.ValueObjects;
using ARGOS.SharedKernel.Abstractions;

namespace ARGOS.Domain.Aggregates.OrganizationSetup.Events;

public sealed record OrganizationSetupCreatedEvent(
    OrganizationSetupId SetupId,
    OrganizationId OrganizationId,
    int Version,
    string ActorReference,
    DateTime OccurredOn) : IDomainEvent;
