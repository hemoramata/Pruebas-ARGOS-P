using ARGOS.Domain.Aggregates.Organization.ValueObjects;
using ARGOS.Domain.Aggregates.OrganizationSetup.ValueObjects;
using ARGOS.SharedKernel.Abstractions;

namespace ARGOS.Domain.Aggregates.OrganizationSetup.Events;

public sealed record OrganizationSetupPublishedEvent(
    OrganizationSetupId SetupId,
    OrganizationId OrganizationId,
    int Version,
    OrganizationSetupId? RetiredSetupId,
    string ActorReference,
    string AuthorizationReference,
    DateTime OccurredOn) : IDomainEvent;
