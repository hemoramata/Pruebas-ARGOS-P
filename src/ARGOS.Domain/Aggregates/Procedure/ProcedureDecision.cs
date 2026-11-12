using ARGOS.Domain.Aggregates.Procedure.Enums;

namespace ARGOS.Domain.Aggregates.Procedure;

/// <summary>
/// Immutable audit record for a lifecycle decision over a procedure version.
/// </summary>
public sealed class ProcedureDecision
{
    private ProcedureDecision()
    {
    }

    internal ProcedureDecision(
        ProcedureDecisionType type,
        ProcedureDecisionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Id = Guid.NewGuid();
        Type = type;
        ActorReference = context.ActorReference;
        AssignmentReference = context.AssignmentReference;
        PositionReference = context.PositionReference;
        CompetenceCode = context.CompetenceCode;
        PolicyVersion = context.PolicyVersion;
        AuthorizationReference = context.AuthorizationReference;
        Comments = context.Comments;
        OccurredAtUtc = context.OccurredAtUtc;
    }

    public Guid Id { get; private set; }

    public ProcedureDecisionType Type { get; private set; }

    public string ActorReference { get; private set; } = string.Empty;

    public string AssignmentReference { get; private set; } = string.Empty;

    public string PositionReference { get; private set; } = string.Empty;

    public string CompetenceCode { get; private set; } = string.Empty;

    public string PolicyVersion { get; private set; } = string.Empty;

    public string AuthorizationReference { get; private set; } = string.Empty;

    public string? Comments { get; private set; }

    public DateTime OccurredAtUtc { get; private set; }
}
