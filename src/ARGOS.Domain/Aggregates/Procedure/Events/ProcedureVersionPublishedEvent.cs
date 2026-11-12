using ARGOS.Domain.Aggregates.Procedure.ValueObjects;
using ARGOS.SharedKernel.Abstractions;

namespace ARGOS.Domain.Aggregates.Procedure.Events;

/// <summary>
/// Raised when a procedure version becomes operational and optionally retires another.
/// </summary>
public sealed record ProcedureVersionPublishedEvent(
    ProcedureDefinitionId DefinitionId,
    ProcedureId PublishedVersionId,
    ProcedureId? RetiredVersionId,
    bool IsEmergencyPublication,
    DateTime OccurredOn) : IDomainEvent;
