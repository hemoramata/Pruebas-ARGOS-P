namespace ARGOS.Application.Security;

/// <summary>Single-node, single-use MFA bound to an exact server-computed operation fingerprint.</summary>
public sealed class OperationVerification(ILoginAccounts accounts, IVerificationDelivery delivery,
    ActiveSessions sessions, TimeProvider clock) : IDisposable
{
    private sealed record Pending(Guid Organization, string Permission, SecondFactorChallenge Challenge);
    private readonly Dictionary<Guid, Pending> pending = [];
    private readonly Dictionary<Guid, DateTimeOffset> lastIssue = [];
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<LoginChallenge?> BeginAsync(Guid person, Guid organization, Guid session,
        string permission, string fingerprint, CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            var account = await accounts.GetActiveAsync(person, ct);
            if (!CanAct(account, person, organization, session, permission)) return null;
            var now = clock.GetUtcNow();
            foreach (var id in pending.Where(x => x.Value.Challenge.ExpiresAt <= now).Select(x => x.Key).ToArray())
            { pending[id].Challenge.Dispose(); pending.Remove(id); }
            foreach (var id in lastIssue.Where(x => x.Value.AddMinutes(1) <= now).Select(x => x.Key).ToArray()) lastIssue.Remove(id);
            if (pending.Count >= 1000 || lastIssue.ContainsKey(person)) return null;
            lastIssue[person] = now;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            var challenge = await SecondFactorChallenge.IssueAsync(person, session, "operation:" + fingerprint,
                account!.Channel, TimeSpan.FromMinutes(3), 5,
                (code, token) => delivery.SendAsync(person, account.Channel, code, token), clock, timeout.Token);
            pending.Add(challenge.Id, new(organization, permission, challenge));
            return new(challenge.Id, session, account.MaskedDestination, challenge.ExpiresAt);
        }
        finally { gate.Release(); }
    }

    public async Task<bool> ConsumeAsync(Guid person, Guid organization, Guid session, string permission,
        string fingerprint, Guid challengeId, string? code, CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            if (!pending.TryGetValue(challengeId, out var item) || item.Organization != organization || item.Permission != permission)
                return false;
            if (!CanAct(await accounts.GetActiveAsync(person, ct), person, organization, session, permission)) return false;
            var result = item.Challenge.Verify(person, session, "operation:" + fingerprint, code);
            if (result != VerificationResult.Rejected) { pending.Remove(challengeId); item.Challenge.Dispose(); }
            return result == VerificationResult.Accepted;
        }
        finally { gate.Release(); }
    }

    private bool CanAct(LoginAccount? account, Guid person, Guid organization, Guid session, string permission) =>
        account is not null && account.PersonId == person && account.OrganizationId == organization
        && !account.MustChangePassword && sessions.Contains(session, person) && account.Permissions.Contains(permission);

    public void Dispose() { foreach (var item in pending.Values) item.Challenge.Dispose(); gate.Dispose(); }
}
