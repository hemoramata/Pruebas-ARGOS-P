using ARGOS.Application.Security;

namespace ARGOS.UnitTests;

public sealed class SecondFactorChallengeTests
{
    private readonly Guid person = Guid.NewGuid();
    private readonly Guid session = Guid.NewGuid();
    private const string Purpose = "login";
    private string sent = "";
    private readonly ManualClock clock = new();

    private Task<SecondFactorChallenge> Issue(VerificationChannel channel = VerificationChannel.Sms) =>
        SecondFactorChallenge.IssueAsync(person, session, Purpose, channel,
            TimeSpan.FromMinutes(3), 3, (code, _) => { sent = code; return Task.CompletedTask; }, clock);

    [Theory]
    [InlineData(VerificationChannel.Sms)]
    [InlineData(VerificationChannel.InstitutionalEmail)]
    public async Task DeliveredCodeIsAcceptedOnlyOnce(VerificationChannel channel)
    {
        using var challenge = await Issue(channel);
        Assert.Matches("^[0-9]{6}$", sent);
        Assert.Equal(VerificationResult.Accepted, challenge.Verify(person, session, Purpose, sent));
        Assert.Equal(VerificationResult.Unavailable, challenge.Verify(person, session, Purpose, sent));
    }

    [Fact]
    public async Task ExpiryBoundaryRejectsCorrectCode()
    {
        using var challenge = await Issue();
        clock.Now = challenge.ExpiresAt;
        Assert.Equal(VerificationResult.Expired, challenge.Verify(person, session, Purpose, sent));
    }

    [Fact]
    public async Task WrongBindingsConsumeAttemptsAndLock()
    {
        using var challenge = await Issue();
        Assert.Equal(VerificationResult.Rejected, challenge.Verify(Guid.NewGuid(), session, Purpose, sent));
        Assert.Equal(VerificationResult.Rejected, challenge.Verify(person, Guid.NewGuid(), Purpose, sent));
        Assert.Equal(VerificationResult.Locked, challenge.Verify(person, session, "other", sent));
        Assert.Equal(VerificationResult.Unavailable, challenge.Verify(person, session, Purpose, sent));
    }

    [Fact]
    public async Task WrongAndMalformedCodesConsumeAttempts()
    {
        using var challenge = await Issue();
        var wrong = sent == "000000" ? "000001" : "000000";
        Assert.Equal(VerificationResult.Rejected, challenge.Verify(person, session, Purpose, wrong));
        Assert.Equal(VerificationResult.Rejected, challenge.Verify(person, session, Purpose, null));
        Assert.Equal(VerificationResult.Locked, challenge.Verify(person, session, Purpose, "1234567"));
    }

    [Fact]
    public async Task InvalidatedChallengeCannotBeUsed()
    {
        using var challenge = await Issue();
        challenge.Dispose();
        Assert.Equal(VerificationResult.Unavailable, challenge.Verify(person, session, Purpose, sent));
    }

    [Fact]
    public async Task ConcurrentVerificationHasExactlyOneWinner()
    {
        using var challenge = await Issue();
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(
            () => challenge.Verify(person, session, Purpose, sent))));
        Assert.Single(results, result => result == VerificationResult.Accepted);
    }

    [Fact]
    public async Task DeliveryFailureDoesNotReturnChallenge()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => SecondFactorChallenge.IssueAsync(
            person, session, Purpose, VerificationChannel.Sms, TimeSpan.FromMinutes(3), 3,
            (_, _) => throw new InvalidOperationException("Simulated delivery failure"), clock));
    }

    [Fact]
    public async Task SlowDeliveryDoesNotActivateExpiredChallenge()
    {
        await Assert.ThrowsAsync<TimeoutException>(() => SecondFactorChallenge.IssueAsync(
            person, session, Purpose, VerificationChannel.Sms, TimeSpan.FromMinutes(3), 3,
            (_, _) => { clock.Now = clock.Now.AddMinutes(3); return Task.CompletedTask; }, clock));
    }

    [Fact]
    public async Task CancellationAfterDeliveryDoesNotActivateChallenge()
    {
        using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SecondFactorChallenge.IssueAsync(
            person, session, Purpose, VerificationChannel.Sms, TimeSpan.FromMinutes(3), 3,
            (_, _) => { cancellation.Cancel(); return Task.CompletedTask; }, clock, cancellation.Token));
    }

    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
