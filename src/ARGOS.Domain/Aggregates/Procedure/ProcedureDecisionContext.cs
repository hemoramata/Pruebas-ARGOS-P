using ARGOS.Domain.Aggregates.Procedure.Errors;
using ARGOS.SharedKernel.Results;

namespace ARGOS.Domain.Aggregates.Procedure;

/// <summary>
/// Identifies who performed a procedure decision and under which authorization.
/// </summary>
public sealed record ProcedureDecisionContext
{
    private ProcedureDecisionContext(
        string actorReference,
        string assignmentReference,
        string positionReference,
        string competenceCode,
        string policyVersion,
        string authorizationReference,
        string? comments,
        DateTime occurredAtUtc)
    {
        ActorReference = actorReference;
        AssignmentReference = assignmentReference;
        PositionReference = positionReference;
        CompetenceCode = competenceCode;
        PolicyVersion = policyVersion;
        AuthorizationReference = authorizationReference;
        Comments = comments;
        OccurredAtUtc = occurredAtUtc;
    }

    public string ActorReference { get; }

    public string AssignmentReference { get; }

    public string PositionReference { get; }

    public string CompetenceCode { get; }

    public string PolicyVersion { get; }

    public string AuthorizationReference { get; }

    public string? Comments { get; }

    public DateTime OccurredAtUtc { get; }

    public static Result<ProcedureDecisionContext> Create(
        string? actorReference,
        string? assignmentReference,
        string? positionReference,
        string? competenceCode,
        string? policyVersion,
        string? authorizationReference,
        string? comments,
        DateTime occurredAtUtc)
    {
        if (IsMissingOrTooLong(actorReference, 100)
            || IsMissingOrTooLong(assignmentReference, 100)
            || IsMissingOrTooLong(positionReference, 100)
            || IsMissingOrTooLong(competenceCode, 100)
            || IsMissingOrTooLong(policyVersion, 100)
            || IsMissingOrTooLong(authorizationReference, 200)
            || comments?.Length > 2000
            || occurredAtUtc.Kind != DateTimeKind.Utc)
        {
            return Result<ProcedureDecisionContext>.Failure(
                ProcedureErrors.InvalidDecisionContext);
        }

        return Result<ProcedureDecisionContext>.Success(new ProcedureDecisionContext(
            actorReference!.Trim(),
            assignmentReference!.Trim(),
            positionReference!.Trim(),
            competenceCode!.Trim().ToUpperInvariant(),
            policyVersion!.Trim(),
            authorizationReference!.Trim(),
            string.IsNullOrWhiteSpace(comments) ? null : comments.Trim(),
            occurredAtUtc));
    }

    private static bool IsMissingOrTooLong(string? value, int maximumLength)
        => string.IsNullOrWhiteSpace(value) || value.Trim().Length > maximumLength;
}
