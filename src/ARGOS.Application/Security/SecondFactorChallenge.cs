using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ARGOS.Application.Security;

public enum VerificationChannel { Sms, InstitutionalEmail }
public enum VerificationResult { Accepted, Rejected, Expired, Locked, Unavailable }

/// <summary>
/// Server-side, in-memory challenge primitive. Not a session issuer or persistent store.
/// Delivery must succeed before activation. Callers must enforce issuance rate limits.
/// </summary>
public sealed class SecondFactorChallenge : IDisposable
{
    private readonly object gate = new();
    private readonly byte[] key = RandomNumberGenerator.GetBytes(32);
    private byte[] digest = [];
    private readonly Guid personId;
    private readonly Guid sessionId;
    private readonly string purpose;
    private readonly TimeProvider clock;
    private readonly int maxAttempts;
    private bool active;
    private bool ended;
    private int attempts;

    public Guid Id { get; } = Guid.NewGuid();
    public DateTimeOffset ExpiresAt { get; }
    public VerificationChannel Channel { get; }

    private SecondFactorChallenge(Guid personId, Guid sessionId, string purpose,
        VerificationChannel channel, TimeSpan lifetime, int maxAttempts, TimeProvider clock)
    {
        if (personId == Guid.Empty || sessionId == Guid.Empty)
            throw new ArgumentException("Person and session identifiers are required.");
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        if (!Enum.IsDefined(channel)) throw new ArgumentOutOfRangeException(nameof(channel));
        if (lifetime <= TimeSpan.Zero || lifetime > TimeSpan.FromMinutes(5))
            throw new ArgumentOutOfRangeException(nameof(lifetime));
        if (maxAttempts is < 1 or > 5) throw new ArgumentOutOfRangeException(nameof(maxAttempts));
        this.personId = personId;
        this.sessionId = sessionId;
        this.purpose = purpose;
        this.maxAttempts = maxAttempts;
        this.clock = clock;
        Channel = channel;
        ExpiresAt = clock.GetUtcNow().Add(lifetime);
    }

    /// <summary>
    /// The trusted delivery adapter receives the code only for transmission. It must
    /// resolve a previously verified destination server-side and never log the code.
    /// No challenge is returned on failed delivery. Do not expose this method as an API.
    /// </summary>
    public static async Task<SecondFactorChallenge> IssueAsync(Guid personId, Guid sessionId,
        string purpose, VerificationChannel channel, TimeSpan lifetime, int maxAttempts,
        Func<string, CancellationToken, Task> deliver, TimeProvider? clock = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(deliver);
        cancellationToken.ThrowIfCancellationRequested();
        var challenge = new SecondFactorChallenge(personId, sessionId, purpose, channel,
            lifetime, maxAttempts, clock ?? TimeProvider.System);
        try
        {
            var code = RandomNumberGenerator.GetInt32(1_000_000).ToString("D6", CultureInfo.InvariantCulture);
            challenge.digest = HMACSHA256.HashData(challenge.key, Encoding.UTF8.GetBytes(code));
            await deliver(code, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (challenge.clock.GetUtcNow() >= challenge.ExpiresAt)
                throw new TimeoutException("Verification delivery exceeded challenge lifetime.");
            challenge.active = true;
            return challenge;
        }
        catch
        {
            challenge.Dispose();
            throw;
        }
    }

    public VerificationResult Verify(Guid person, Guid session, string challengePurpose, string? code)
    {
        lock (gate)
        {
            if (ended || !active) return VerificationResult.Unavailable;
            if (clock.GetUtcNow() >= ExpiresAt)
            {
                End();
                return VerificationResult.Expired;
            }
            // Every submission consumes an attempt, including wrong binding and malformed input.
            attempts++;
            var valid = false;
            if (person == personId && session == sessionId && challengePurpose == purpose
                && code is { Length: 6 } && code.All(c => c is >= '0' and <= '9'))
            {
                var supplied = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(code));
                valid = CryptographicOperations.FixedTimeEquals(digest, supplied);
                CryptographicOperations.ZeroMemory(supplied);
            }
            if (valid)
            {
                End();
                return VerificationResult.Accepted;
            }
            if (attempts >= maxAttempts)
            {
                End();
                return VerificationResult.Locked;
            }
            return VerificationResult.Rejected;
        }
    }

    /// <summary>Invalidate on replacement, cancellation or session revocation.</summary>
    public void Dispose()
    {
        lock (gate) End();
    }

    private void End()
    {
        ended = true;
        active = false;
        CryptographicOperations.ZeroMemory(key);
        CryptographicOperations.ZeroMemory(digest);
    }
}
