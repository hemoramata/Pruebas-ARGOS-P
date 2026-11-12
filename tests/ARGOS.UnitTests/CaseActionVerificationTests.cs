using ARGOS.Application.Security;

namespace ARGOS.UnitTests;

public sealed class CaseActionVerificationTests
{
    [Fact]
    public async Task CodeIsBoundToPersonSessionCaseActionAndResult()
    {
        using var f = new Fixture();
        var c = (await f.Issue())!;
        var otherPerson = Guid.NewGuid();
        var otherSession = Guid.NewGuid();
        f.Sessions.Add(otherSession, otherPerson);
        Assert.False(await f.Service.ConsumeAsync(otherPerson, otherSession, f.Case, "finalize", "APPROVED", c.Id, f.Code, default));
        Assert.False(await f.Service.ConsumeAsync(f.Person, f.Session, Guid.NewGuid(), "finalize", "APPROVED", c.Id, f.Code, default));
        Assert.False(await f.Service.ConsumeAsync(f.Person, f.Session, f.Case, "archive", null, c.Id, f.Code, default));
        Assert.False(await f.Service.ConsumeAsync(f.Person, f.Session, f.Case, "finalize", "DENIED", c.Id, f.Code, default));
        Assert.True(await f.Consume(c));
        Assert.False(await f.Consume(c));
    }

    [Fact]
    public async Task ConcurrentConsumptionAcceptsExactlyOnce()
    {
        using var f = new Fixture();
        var c = (await f.Issue())!;
        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => f.Consume(c)));
        Assert.Single(results, x => x);
    }

    [Theory]
    [InlineData("permission")]
    [InlineData("account")]
    [InlineData("session")]
    [InlineData("expiry")]
    [InlineData("provisional")]
    [InlineData("organization")]
    public async Task RevocationAndExpiryPreventConsumption(string reason)
    {
        using var f = new Fixture();
        var c = (await f.Issue())!;
        if (reason == "permission") f.Permissions = [];
        if (reason == "account") f.Active = false;
        if (reason == "session") f.Sessions.Revoke(f.Session);
        if (reason == "expiry") f.Clock.Advance(TimeSpan.FromMinutes(3));
        if (reason == "provisional") f.MustChangePassword = true;
        if (reason == "organization") f.Organization = Guid.NewGuid();
        Assert.False(await f.Consume(c));
    }

    [Fact]
    public async Task ProvisionalPasswordCannotRequestAdditionalVerification()
    {
        using var f = new Fixture { MustChangePassword = true };
        Assert.Null(await f.Issue());
        Assert.Empty(f.Code);
    }

    [Theory]
    [InlineData("session")]
    [InlineData("permission")]
    [InlineData("provisional")]
    public async Task RevocationDuringDeliveryDoesNotReturnUsableChallenge(string reason)
    {
        using var f = new Fixture();
        f.DuringDelivery = () => {
            if (reason == "session") f.Sessions.Revoke(f.Session);
            if (reason == "permission") f.Permissions = [];
            if (reason == "provisional") f.MustChangePassword = true;
        };
        Assert.Null(await f.Issue());
    }

    [Fact]
    public async Task ReissueIsThrottledAndReplacesTheOldCode()
    {
        using var f = new Fixture();
        var first = (await f.Issue())!;
        var firstCode = f.Code;
        Assert.Null(await f.Issue());
        f.Clock.Advance(TimeSpan.FromMinutes(1));
        var second = (await f.Issue())!;
        Assert.False(await f.Service.ConsumeAsync(f.Person, f.Session, f.Case, "finalize", "APPROVED", first.Id, firstCode, default));
        Assert.True(await f.Consume(second));
    }

    [Fact]
    public async Task FiveWrongCodesLockTheChallenge()
    {
        using var f = new Fixture();
        var c = (await f.Issue())!;
        for (var i = 0; i < 5; i++)
            Assert.False(await f.Service.ConsumeAsync(f.Person, f.Session, f.Case, "finalize", "APPROVED", c.Id, "wrong", default));
        Assert.False(await f.Consume(c));
    }

    [Fact]
    public async Task FailedDeliveryDoesNotIssueAChallenge()
    {
        using var f = new Fixture { FailDelivery = true };
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Issue());
        Assert.Null(await f.Issue());
    }

    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan amount) => now += amount;
    }

    private sealed class Fixture : ILoginAccounts, IVerificationDelivery, IDisposable
    {
        public readonly Guid Person = Guid.NewGuid(), Session = Guid.NewGuid(), Case = Guid.NewGuid();
        public Guid Organization = Guid.NewGuid();
        public readonly TestClock Clock = new();
        public readonly ActiveSessions Sessions;
        public readonly CaseActionVerification Service;
        public bool Active = true, FailDelivery, MustChangePassword;
        public Action? DuringDelivery;
        public string[] Permissions = ["case-files.finalize", "case-files.archive"];
        public string Code = "";
        public Fixture()
        {
            Sessions = new(Clock);
            Sessions.Add(Session, Person);
            Service = new(this, this, Sessions, Clock);
        }
        public Task<LoginChallenge?> Issue() => Service.BeginAsync(Person, Session, Case, "finalize", "APPROVED", default);
        public Task<bool> Consume(LoginChallenge c) => Service.ConsumeAsync(Person, Session, Case, "finalize", "APPROVED", c.Id, Code, default);
        public Task<LoginAccount?> GetActiveAsync(Guid personId, CancellationToken ct) => Task.FromResult<LoginAccount?>(
            Active ? new(personId, Organization, "Test", "***1234", VerificationChannel.Sms, Permissions, MustChangePassword) : null);
        public Task<LoginAccount?> ValidatePasswordAsync(string identification, string password, CancellationToken ct) => GetActiveAsync(Person, ct);
        public Task SendAsync(Guid person, VerificationChannel channel, string code, CancellationToken ct)
        {
            if (FailDelivery) throw new InvalidOperationException("Simulated unavailable provider");
            Code = code;
            DuringDelivery?.Invoke();
            return Task.CompletedTask;
        }
        public void Dispose() => Service.Dispose();
    }
}
