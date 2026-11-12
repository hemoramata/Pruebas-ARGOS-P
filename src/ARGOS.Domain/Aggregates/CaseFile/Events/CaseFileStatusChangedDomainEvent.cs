using ARGOS.Domain.Aggregates.CaseFile.Enums;
using ARGOS.SharedKernel.Abstractions;

namespace ARGOS.Domain.Aggregates.CaseFile.Events;

/// <summary>
/// Records an authorized lifecycle transition of a case file.
/// </summary>
public sealed record CaseFileStatusChangedDomainEvent(
    Guid CaseFileId,
    CaseFileStatus PreviousStatus,
    CaseFileStatus NewStatus,
    string Reason) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
