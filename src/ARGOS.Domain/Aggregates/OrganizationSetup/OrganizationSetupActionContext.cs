using ARGOS.Domain.Aggregates.OrganizationSetup.Errors;
using ARGOS.SharedKernel.Results;

namespace ARGOS.Domain.Aggregates.OrganizationSetup;

public sealed record OrganizationSetupActionContext
{
    private OrganizationSetupActionContext(
        string actorReference,
        string policyVersion,
        string authorizationReference,
        DateTime occurredAtUtc)
    {
        ActorReference = actorReference;
        PolicyVersion = policyVersion;
        AuthorizationReference = authorizationReference;
        OccurredAtUtc = occurredAtUtc;
    }

    public string ActorReference { get; }

    public string PolicyVersion { get; }

    public string AuthorizationReference { get; }

    public DateTime OccurredAtUtc { get; }

    public static Result<OrganizationSetupActionContext> Create(
        string? actorReference,
        string? policyVersion,
        string? authorizationReference,
        DateTime occurredAtUtc)
    {
        if (string.IsNullOrWhiteSpace(actorReference)
            || actorReference.Trim().Length > 100
            || string.IsNullOrWhiteSpace(policyVersion)
            || policyVersion.Trim().Length > 100
            || string.IsNullOrWhiteSpace(authorizationReference)
            || authorizationReference.Trim().Length > 200
            || occurredAtUtc.Kind != DateTimeKind.Utc)
        {
            return Result<OrganizationSetupActionContext>.Failure(
                OrganizationSetupErrors.InvalidActionContext);
        }

        return Result<OrganizationSetupActionContext>.Success(
            new OrganizationSetupActionContext(
                actorReference.Trim(),
                policyVersion.Trim(),
                authorizationReference.Trim(),
                occurredAtUtc));
    }
}
