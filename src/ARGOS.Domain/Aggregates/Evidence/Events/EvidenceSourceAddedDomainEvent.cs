using ARGOS.Domain.Aggregates.Evidence.ValueObjects;
using ARGOS.SharedKernel.Abstractions;

namespace ARGOS.Domain.Aggregates.Evidence.Events;

public sealed record EvidenceSourceAddedDomainEvent(
    EvidenceId EvidenceId,
    EvidenceSourceId SourceId) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
