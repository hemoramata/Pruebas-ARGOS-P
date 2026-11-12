using System.Collections.Concurrent;

namespace ARGOS.Application.Security;

/// <summary>Personal level-zero account with an external recovery/MFA contact. It never bypasses institutional approval.</summary>
public sealed class SuperuserAccount
{
    private SuperuserAccount() { }
    public Guid PersonId { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Identification { get; private set; } = "";
    public string FullName { get; private set; } = "";
    public string ExternalEmail { get; private set; } = "";
    public string EmailVerificationReference { get; private set; } = "";
    public bool Enabled { get; private set; }
    public int AccessLevel => 0;
    public static SuperuserAccount Provision(Guid personId, Guid organizationId, string identification, string fullName,
        string email, string emailVerificationReference)
    {
        if (personId == Guid.Empty) throw new ArgumentException("Person is required.");
        if (string.IsNullOrWhiteSpace(identification) || identification.Trim().Length > 100
            || string.IsNullOrWhiteSpace(fullName) || fullName.Trim().Length > 200
            || string.IsNullOrWhiteSpace(email) || !email.Contains('@') || email.Trim().Length > 254
            || string.IsNullOrWhiteSpace(emailVerificationReference) || emailVerificationReference.Trim().Length > 200)
            throw new ArgumentException("Superuser identity is invalid.");
        return new() { PersonId = personId, OrganizationId = organizationId,
            Identification = identification.Trim(), FullName = fullName.Trim(), ExternalEmail = email.Trim(),
            EmailVerificationReference = emailVerificationReference.Trim(), Enabled = true };
    }
    public void Disable() => Enabled = false;
}

/// <summary>Institutional authority whose verified email can approve a level-zero action.</summary>
public sealed class InstitutionalAuthority
{
    private InstitutionalAuthority() { }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string FullName { get; private set; } = "";
    public string Email { get; private set; } = "";
    public string VerificationReference { get; private set; } = "";
    public bool Enabled { get; private set; }
    public static InstitutionalAuthority Register(Guid organizationId, string fullName, string email, string verificationReference)
    {
        if (organizationId == Guid.Empty || string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email)
            || !email.Contains('@') || string.IsNullOrWhiteSpace(verificationReference))
            throw new ArgumentException("Verified institutional authority is required.");
        return new() { Id = Guid.NewGuid(), OrganizationId = organizationId, FullName = fullName.Trim(),
            Email = email.Trim(), VerificationReference = verificationReference.Trim(), Enabled = true };
    }
    public void Disable() => Enabled = false;
}

public interface IAuthorityApprovalDelivery
{
    Task SendAsync(string destination, string code, CancellationToken ct);
}

public sealed class UnconfiguredAuthorityApprovalDelivery : IAuthorityApprovalDelivery
{
    public Task SendAsync(string destination, string code, CancellationToken ct)
        => throw new InvalidOperationException("Institutional authority approval delivery is not configured.");
}

public sealed record SuperuserAuthorization(Guid Id, Guid SuperuserId, Guid AuthorityId,
    string Action, string SubjectType, string SubjectReference, DateTimeOffset ExpiresAt);

/// <summary>Every level-zero action, including reads, requires a fresh authority email approval.</summary>
public sealed class SuperuserAuthorizationService(IAuthorityApprovalDelivery delivery, TimeProvider clock) : IDisposable
{
    private sealed record Pending(Guid Superuser, Guid Authority, string Action, string SubjectType,
        string SubjectReference, string Purpose, SecondFactorChallenge Challenge);
    private readonly ConcurrentDictionary<Guid, Pending> pending = new();
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> lastIssue = new();

    public async Task<SuperuserAuthorization?> BeginAsync(SuperuserAccount superuser, InstitutionalAuthority authority,
        Guid sessionId, string action, string subjectType, string subjectReference, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(superuser); ArgumentNullException.ThrowIfNull(authority);
        if (!superuser.Enabled || !authority.Enabled
            || (superuser.OrganizationId != Guid.Empty && superuser.OrganizationId != authority.OrganizationId)
            || sessionId == Guid.Empty || string.IsNullOrWhiteSpace(superuser.ExternalEmail)
            || string.IsNullOrWhiteSpace(superuser.EmailVerificationReference))
            return null;
        var normalizedAction = Required(action, 120);
        var normalizedType = Required(subjectType, 120);
        var normalizedReference = Required(subjectReference, 240);
        var now = clock.GetUtcNow();
        if (lastIssue.TryGetValue(superuser.PersonId, out var last) && now < last.AddMinutes(1)) return null;
        lastIssue[superuser.PersonId] = now;
        foreach (var item in pending.Where(x => x.Value.Superuser == superuser.PersonId).ToArray())
            if (pending.TryRemove(item.Key, out _)) item.Value.Challenge.Dispose();
        var purpose = $"superuser:{superuser.PersonId:D}:{authority.Id:D}:{normalizedAction}:{normalizedType}:{normalizedReference}";
        var challenge = await SecondFactorChallenge.IssueAsync(superuser.PersonId, sessionId, purpose,
            VerificationChannel.InstitutionalEmail, TimeSpan.FromMinutes(3), 5,
            (code, token) => delivery.SendAsync(authority.Email, code, token), clock, ct);
        pending[challenge.Id] = new(superuser.PersonId, authority.Id, normalizedAction,
            normalizedType, normalizedReference, purpose, challenge);
        return new(challenge.Id, superuser.PersonId, authority.Id, normalizedAction, normalizedType, normalizedReference, challenge.ExpiresAt);
    }

    public bool Complete(SuperuserAccount superuser, InstitutionalAuthority authority, Guid sessionId,
        SuperuserAuthorization authorization, string? code)
    {
        if (!superuser.Enabled || !authority.Enabled || authorization.SuperuserId != superuser.PersonId
            || authorization.AuthorityId != authority.Id || !pending.TryGetValue(authorization.Id, out var item)
            || authorization.Action != item.Action || authorization.SubjectType != item.SubjectType
            || authorization.SubjectReference != item.SubjectReference
            || authorization.ExpiresAt <= clock.GetUtcNow() || authorization.ExpiresAt > item.Challenge.ExpiresAt)
            return false;
        var result = item.Challenge.Verify(superuser.PersonId, sessionId, item.Purpose, code);
        if (result != VerificationResult.Accepted)
        {
            if (result != VerificationResult.Rejected) pending.TryRemove(authorization.Id, out _);
            return false;
        }
        pending.TryRemove(authorization.Id, out _);
        item.Challenge.Dispose();
        return true;
    }

    public void Dispose() { foreach (var item in pending.Values) item.Challenge.Dispose(); }
    private static string Required(string value, int max) => string.IsNullOrWhiteSpace(value) || value.Trim().Length > max
        ? throw new ArgumentException("Authorization value is invalid.") : value.Trim();
}
