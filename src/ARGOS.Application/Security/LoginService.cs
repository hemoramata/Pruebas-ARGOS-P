namespace ARGOS.Application.Security;

public sealed record LoginAccount(Guid PersonId, Guid OrganizationId, string FullName, string MaskedDestination,
    VerificationChannel Channel, IReadOnlyCollection<string> Permissions, bool MustChangePassword = false, string CredentialVersion = "");

/// <summary>Implementations validate credentials, verified contacts and current assignment on the server.</summary>
public interface ILoginAccounts
{
    Task<LoginAccount?> ValidatePasswordAsync(string identification, string password, CancellationToken ct);
    Task<LoginAccount?> GetActiveAsync(Guid personId, CancellationToken ct);
}

public interface IPasswordChanger
{
    Task<bool> ChangePasswordAsync(Guid personId, string currentPassword, string newPassword, CancellationToken ct);
}

public sealed class UnconfiguredPasswordChanger : IPasswordChanger
{
    public Task<bool> ChangePasswordAsync(Guid personId, string currentPassword, string newPassword, CancellationToken ct)
        => Task.FromResult(false);
}

public interface IVerificationDelivery
{
    Task SendAsync(Guid personId, VerificationChannel channel, string code, CancellationToken ct);
}

public sealed record LoginChallenge(Guid Id, Guid Binding, string MaskedDestination, DateTimeOffset ExpiresAt);

/// <summary>Single-node prototype. Restarts invalidate pending challenges. Never stores passwords.</summary>
public sealed class LoginService(ILoginAccounts accounts, IVerificationDelivery delivery, TimeProvider clock) : IDisposable
{
    private sealed record Pending(Guid PersonId, Guid Binding, SecondFactorChallenge Challenge, string CredentialVersion);
    private readonly Dictionary<Guid, Pending> pending = [];
    private readonly Dictionary<Guid, DateTimeOffset> lastIssue = [];
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<LoginChallenge?> BeginAsync(string identification, string password, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(identification) || identification.Length > 100
            || string.IsNullOrEmpty(password) || password.Length > 1024) return null;
        var account = await accounts.ValidatePasswordAsync(identification, password, ct);
        if (account is null) return null;
        await gate.WaitAsync(ct);
        try
        {
            var now = clock.GetUtcNow();
            foreach (var id in pending.Where(p => p.Value.Challenge.ExpiresAt <= now).Select(p => p.Key).ToArray())
            {
                pending[id].Challenge.Dispose();
                pending.Remove(id);
            }
            foreach (var id in lastIssue.Where(p => p.Value.AddMinutes(1) <= now).Select(p => p.Key).ToArray())
                lastIssue.Remove(id);
            if (pending.Count >= 1000 || lastIssue.ContainsKey(account.PersonId)) return null;
            lastIssue[account.PersonId] = now;
            foreach (var id in pending.Where(p => p.Value.PersonId == account.PersonId).Select(p => p.Key).ToArray())
            {
                pending[id].Challenge.Dispose();
                pending.Remove(id);
            }
            var binding = Guid.NewGuid();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            var challenge = await SecondFactorChallenge.IssueAsync(account.PersonId, binding, "login", account.Channel,
                TimeSpan.FromMinutes(3), 5, (code, token) => delivery.SendAsync(account.PersonId, account.Channel, code, token),
                clock, timeout.Token);
            pending.Add(challenge.Id, new Pending(account.PersonId, binding, challenge, account.CredentialVersion));
            return new LoginChallenge(challenge.Id, binding, account.MaskedDestination, challenge.ExpiresAt);
        }
        finally { gate.Release(); }
    }

    public async Task<LoginAccount?> CompleteAsync(Guid id, Guid binding, string code, CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            if (!pending.TryGetValue(id, out var item)) return null;
            var result = item.Challenge.Verify(item.PersonId, binding, "login", code);
            if (result == VerificationResult.Rejected) return null;
            pending.Remove(id);
            item.Challenge.Dispose();
            if (result != VerificationResult.Accepted) return null;
            // Recheck the assignment after MFA: a revoked account must not receive a session.
            var active = await accounts.GetActiveAsync(item.PersonId, ct);
            return active?.CredentialVersion == item.CredentialVersion ? active : null;
        }
        finally { gate.Release(); }
    }

    public async Task CancelAsync(Guid id, Guid binding, CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            if (pending.TryGetValue(id, out var item) && item.Binding == binding)
            {
                pending.Remove(id);
                item.Challenge.Dispose();
            }
        }
        finally { gate.Release(); }
    }

    public void Dispose()
    {
        foreach (var item in pending.Values) item.Challenge.Dispose();
        gate.Dispose();
    }
}

/// <summary>Fail-closed defaults until the institution provisions accounts and a delivery provider.</summary>
public sealed class UnconfiguredLoginAccounts : ILoginAccounts
{
    public Task<LoginAccount?> ValidatePasswordAsync(string identification, string password, CancellationToken ct)
        => Task.FromResult<LoginAccount?>(null);
    public Task<LoginAccount?> GetActiveAsync(Guid personId, CancellationToken ct) => Task.FromResult<LoginAccount?>(null);
}

public sealed class UnconfiguredVerificationDelivery : IVerificationDelivery
{
    public Task SendAsync(Guid personId, VerificationChannel channel, string code, CancellationToken ct)
        => throw new InvalidOperationException("Institutional verification delivery is not configured.");
}
