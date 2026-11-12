using ARGOS.Domain.Aggregates.Evidence.ValueObjects;
using ARGOS.SharedKernel.Abstractions;

namespace ARGOS.Domain.Aggregates.CaseFile.Events;

public sealed record CaseFileEvidenceLinkedDomainEvent(
    Guid CaseFileId,
    Guid LinkId,
    EvidenceId EvidenceId,
    string RequirementCode) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
