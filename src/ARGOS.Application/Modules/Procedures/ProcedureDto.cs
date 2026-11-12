using ARGOS.Domain.Aggregates.Procedure;

namespace ARGOS.Application.Modules.Procedures;

public sealed record ProcedureDto(
    Guid Id,
    Guid DefinitionId,
    int Version,
    Guid? PreviousVersionId,
    Guid? NextVersionId,
    string Code,
    string Name,
    string Description,
    string Status,
    string? ApprovalPolicyVersion,
    int RequiredApprovalCount,
    bool SeparationOfDutiesRequired,
    bool EmergencyPublicationAllowed,
    bool EmergencyRatificationRequired,
    bool EmergencyRatificationPending,
    DateTime? EmergencyRatificationDueAtUtc,
    IReadOnlyCollection<ProcedureDecisionDto> Decisions)
{
    public static ProcedureDto From(Procedure procedure)
        => new(
            procedure.Id.Value,
            procedure.DefinitionId.Value,
            procedure.Version,
            procedure.PreviousVersionId?.Value,
            procedure.NextVersionId?.Value,
            procedure.Code,
            procedure.Name,
            procedure.Description,
            procedure.Status.ToString(),
            procedure.ApprovalPolicyVersion,
            procedure.RequiredApprovalCount,
            procedure.SeparationOfDutiesRequired,
            procedure.EmergencyPublicationAllowed,
            procedure.EmergencyRatificationRequired,
            procedure.EmergencyRatificationPending,
            procedure.EmergencyRatificationDueAtUtc,
            procedure.Decisions
                .OrderBy(decision => decision.OccurredAtUtc)
                .Select(ProcedureDecisionDto.From)
                .ToArray());
}

public sealed record ProcedureDecisionDto(
    Guid Id,
    string Type,
    string ActorReference,
    string AssignmentReference,
    string PositionReference,
    string CompetenceCode,
    string PolicyVersion,
    string AuthorizationReference,
    string? Comments,
    DateTime OccurredAtUtc)
{
    public static ProcedureDecisionDto From(ProcedureDecision decision)
        => new(
            decision.Id,
            decision.Type.ToString(),
            decision.ActorReference,
            decision.AssignmentReference,
            decision.PositionReference,
            decision.CompetenceCode,
            decision.PolicyVersion,
            decision.AuthorizationReference,
            decision.Comments,
            decision.OccurredAtUtc);
}
