using ARGOS.Domain.Aggregates.Evidence.ValueObjects;
using ARGOS.SharedKernel.Abstractions;

namespace ARGOS.Domain.Aggregates.Evidence.Events;

public sealed record EvidenceCreatedDomainEvent(
    EvidenceId EvidenceId,
    Guid CustodianOrganizationId,
    string EvidenceTypeCode) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
