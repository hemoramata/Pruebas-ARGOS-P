using ARGOS.Domain.Aggregates.Procedure.ValueObjects;
using ARGOS.SharedKernel.Abstractions;

namespace ARGOS.Domain.Aggregates.Procedure.Events;

/// <summary>
/// Raised when a new immutable version of a procedure is created.
/// </summary>
public sealed record ProcedureVersionCreatedEvent(
    ProcedureDefinitionId DefinitionId,
    ProcedureId ProcedureId,
    int Version,
    ProcedureId PreviousVersionId) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
