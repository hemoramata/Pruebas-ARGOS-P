using System.Collections.Concurrent;

namespace ARGOS.Application.Security;

public sealed record SensitiveAuthorizationChallenge(
    Guid DecisionId,
    Guid ChallengeId,
    Guid Binding,
    DateTimeOffset ExpiresAt);

/// <summary>
/// Coordinates the second verification required by sensitive institutional decisions.
/// Ordinary evidence and workflow actions must not call this service.
/// </summary>
public sealed class SensitiveDecisionService(
    ISensitiveDecisionStore store,
    IVerificationDelivery delivery,
    TimeProvider clock)
{
    private sealed record Pending(Guid DecisionId, Guid OrganizationId, Guid PersonId, Guid Binding,
        SecondFactorChallenge Challenge);

    private readonly ConcurrentDictionary<Guid, Pending> pending = new();

    public async Task<SensitiveDecision> BeginAsync(
        Guid organizationId,
        Guid initiatedBy,
        string operationCode,
        string subjectType,
        string subjectReference,
        CancellationToken ct = default)
    {
        var decision = SensitiveDecision.Begin(
            organizationId, operationCode, subjectType, subjectReference,
            initiatedBy, clock.GetUtcNow());

        await store.AddAsync(decision, ct);
        await store.SaveAsync(ct);
        return decision;
    }

    public async Task<SensitiveAuthorizationChallenge?> BeginAuthorizationAsync(
        Guid organizationId,
        Guid decisionId,
        Guid authorizedBy,
        Guid sessionId,
        VerificationChannel channel,
        CancellationToken ct = default)
    {
        if (authorizedBy == Guid.Empty || sessionId == Guid.Empty)
            return null;

        var decision = await store.GetAsync(organizationId, decisionId, ct);
        if (decision is null || decision.Status != SensitiveDecisionStatus.Pending)
            return null;

        foreach (var item in pending.Values.Where(x =>
            x.DecisionId == decisionId || x.PersonId == authorizedBy).ToArray())
        {
            if (pending.TryRemove(item.Challenge.Id, out _))
                item.Challenge.Dispose();
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));

        var binding = Guid.NewGuid();
        var challenge = await SecondFactorChallenge.IssueAsync(
            authorizedBy, sessionId, "sensitive:" + decisionId.ToString("N"), channel,
            TimeSpan.FromMinutes(3), 5,
            (code, token) => delivery.SendAsync(authorizedBy, channel, code, token),
            clock, timeout.Token);

        pending[challenge.Id] = new Pending(
            decisionId, organizationId, authorizedBy, binding, challenge);

        return new SensitiveAuthorizationChallenge(
            decisionId, challenge.Id, binding, challenge.ExpiresAt);
    }

    public async Task<bool> CompleteAuthorizationAsync(
        Guid organizationId,
        Guid decisionId,
        Guid challengeId,
        Guid binding,
        Guid authorizedBy,
        Guid sessionId,
        string code,
        string verificationReference,
        string authorizationReference,
        CancellationToken ct = default)
    {
        if (!pending.TryGetValue(challengeId, out var item)
            || item.DecisionId != decisionId
            || item.OrganizationId != organizationId
            || item.PersonId != authorizedBy
            || item.Binding != binding)
            return false;

        var result = item.Challenge.Verify(
            authorizedBy, sessionId, "sensitive:" + decisionId.ToString("N"), code);

        if (result == VerificationResult.Rejected)
            return false;

        pending.TryRemove(challengeId, out _);
        item.Challenge.Dispose();

        if (result != VerificationResult.Accepted)
            return false;

        var decision = await store.GetAsync(organizationId, decisionId, ct);
        if (decision is null || decision.Status != SensitiveDecisionStatus.Pending)
            return false;

        decision.Authorize(
            authorizedBy, verificationReference, authorizationReference,
            clock.GetUtcNow());
        await store.SaveAsync(ct);
        return true;
    }

    public async Task<bool> ExecuteAsync(
        Guid organizationId,
        Guid decisionId,
        string executionReference,
        CancellationToken ct = default)
    {
        var decision = await store.GetAsync(organizationId, decisionId, ct);
        if (decision is null || decision.Status != SensitiveDecisionStatus.Authorized)
            return false;

        decision.MarkExecuted(executionReference, clock.GetUtcNow());
        await store.SaveAsync(ct);
        return true;
    }

    public async Task<bool> RejectAsync(
        Guid organizationId,
        Guid decisionId,
        string rejectionReference,
        CancellationToken ct = default)
    {
        var decision = await store.GetAsync(organizationId, decisionId, ct);
        if (decision is null
            || decision.Status is SensitiveDecisionStatus.Executed
                or SensitiveDecisionStatus.Rejected)
            return false;

        decision.Reject(rejectionReference);
        await store.SaveAsync(ct);
        return true;
    }
}
