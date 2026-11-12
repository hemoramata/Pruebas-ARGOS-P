using ARGOS.Domain.Aggregates.Procedure.ValueObjects;
using ARGOS.SharedKernel.Abstractions;

namespace ARGOS.Domain.Aggregates.Procedure.Events;

/// <summary>
/// Raised for every individual approval, including intermediate approvals.
/// </summary>
public sealed record ProcedureApprovalRecordedEvent(
    ProcedureDefinitionId DefinitionId,
    ProcedureId ProcedureId,
    string ActorReference,
    int RecordedApprovals,
    int RequiredApprovals,
    DateTime OccurredOn) : IDomainEvent;
