namespace ARGOS.Application.Security;

/// <summary>Fresh single-use verification for a specific case action on one server node.</summary>
public sealed class CaseActionVerification(ILoginAccounts accounts, IVerificationDelivery delivery,
    ActiveSessions sessions, TimeProvider clock) : IDisposable
{
    private sealed record Pending(Guid Person, Guid Session, Guid Organization, SecondFactorChallenge Challenge);
    private readonly Dictionary<Guid, Pending> pending = [];
    private readonly Dictionary<Guid, DateTimeOffset> lastIssue = [];
    private readonly SemaphoreSlim gate = new(1, 1);

    public static string Permission(string action) => action switch
    {
        "finalize" => "case-files.finalize",
        "archive" => "case-files.archive",
        _ => throw new ArgumentException("Unsupported case action.")
    };

    private static string Purpose(Guid caseId, string action, string? resultCode) =>
        $"case-files:{caseId:D}:{action}:{(action == "finalize" ? resultCode?.Trim() : "")}";

    public async Task<LoginChallenge?> BeginAsync(Guid person, Guid session, Guid caseId,
        string action, string? resultCode, CancellationToken ct)
    {
        var permission = Permission(action);
        await gate.WaitAsync(ct);
        try
        {
            var account = await accounts.GetActiveAsync(person, ct);
            if (!CanAct(account, person, session, permission)) return null;
            var now = clock.GetUtcNow();
            foreach (var id in pending.Where(x => x.Value.Challenge.ExpiresAt <= now).Select(x => x.Key).ToArray())
            { pending[id].Challenge.Dispose(); pending.Remove(id); }
            foreach (var id in lastIssue.Where(x => x.Value.AddMinutes(1) <= now).Select(x => x.Key).ToArray())
                lastIssue.Remove(id);
            if (pending.Count >= 1000 || lastIssue.ContainsKey(person)) return null;
            lastIssue[person] = now;
            foreach (var id in pending.Where(x => x.Value.Person == person && x.Value.Session == session).Select(x => x.Key).ToArray())
            { pending[id].Challenge.Dispose(); pending.Remove(id); }
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            var challenge = await SecondFactorChallenge.IssueAsync(person, session, Purpose(caseId, action, resultCode),
                account!.Channel, TimeSpan.FromMinutes(3), 5,
                (code, token) => delivery.SendAsync(person, account.Channel, code, token), clock, timeout.Token);
            try
            {
                var current = await accounts.GetActiveAsync(person, ct);
                if (!CanAct(current, person, session, permission) || current!.OrganizationId != account.OrganizationId)
                { challenge.Dispose(); return null; }
            }
            catch { challenge.Dispose(); throw; }
            pending.Add(challenge.Id, new(person, session, account.OrganizationId, challenge));
            return new(challenge.Id, session, account.MaskedDestination, challenge.ExpiresAt);
        }
        finally { gate.Release(); }
    }

    public async Task<bool> ConsumeAsync(Guid person, Guid session, Guid caseId, string action,
        string? resultCode, Guid challengeId, string? code, CancellationToken ct)
    {
        var permission = Permission(action);
        await gate.WaitAsync(ct);
        try
        {
            if (!pending.TryGetValue(challengeId, out var item)) return false;
            var account = await accounts.GetActiveAsync(person, ct);
            if (!CanAct(account, person, session, permission) || account!.OrganizationId != item.Organization)
                return false;
            var result = item.Challenge.Verify(person, session, Purpose(caseId, action, resultCode), code);
            if (result != VerificationResult.Rejected)
            { pending.Remove(challengeId); item.Challenge.Dispose(); }
            return result == VerificationResult.Accepted;
        }
        finally { gate.Release(); }
    }

    private bool CanAct(LoginAccount? account, Guid person, Guid session, string permission) =>
        account is not null && account.PersonId == person && !account.MustChangePassword
        && sessions.Contains(session, person) && account.Permissions.Contains(permission);

    public void Dispose()
    {
        foreach (var item in pending.Values) item.Challenge.Dispose();
        gate.Dispose();
    }
}
