using ARGOS.Domain.Aggregates.Procedure.Enums;
using ARGOS.Domain.Aggregates.Procedure.ValueObjects;
using ARGOS.SharedKernel.Abstractions;

namespace ARGOS.Domain.Aggregates.Procedure.Events;

/// <summary>
/// Raised after a traceable lifecycle transition of a procedure version.
/// </summary>
public sealed record ProcedureLifecycleChangedEvent(
    ProcedureDefinitionId DefinitionId,
    ProcedureId ProcedureId,
    ProcedureStatus PreviousStatus,
    ProcedureStatus NewStatus,
    ProcedureDecisionType DecisionType,
    string ActorReference,
    DateTime OccurredOn) : IDomainEvent;
