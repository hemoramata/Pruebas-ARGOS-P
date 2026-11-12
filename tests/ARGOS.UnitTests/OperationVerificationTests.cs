using ARGOS.Application.Security;

namespace ARGOS.UnitTests;

public sealed class OperationVerificationTests
{
    [Fact]
    public async Task VerificationIsBoundToIdentityOrganizationSessionPermissionAndExactPayload()
    {
        using var f = new Fixture();
        var challenge = (await f.Issue())!;
        Assert.False(await f.Service.ConsumeAsync(f.Person, Guid.NewGuid(), f.Session, "procedures.manage", "hash", challenge.Id, f.Code, default));
        Assert.False(await f.Service.ConsumeAsync(f.Person, f.Organization, Guid.NewGuid(), "procedures.manage", "hash", challenge.Id, f.Code, default));
        Assert.False(await f.Service.ConsumeAsync(f.Person, f.Organization, f.Session, "personnel.manage", "hash", challenge.Id, f.Code, default));
        Assert.False(await f.Consume(challenge, "modified"));
        Assert.True(await f.Consume(challenge));
        Assert.False(await f.Consume(challenge));
    }

    [Fact]
    public async Task ConcurrentConsumptionAcceptsOnlyOneRequest()
    {
        using var f = new Fixture();
        var challenge = (await f.Issue())!;
        Assert.Single(await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => f.Consume(challenge))), x => x);
    }

    [Fact]
    public async Task PermissionRevocationPreventsUseAndRepeatedIssuanceIsLimited()
    {
        using var f = new Fixture();
        var challenge = (await f.Issue())!;
        Assert.Null(await f.Issue());
        f.Permissions = [];
        Assert.False(await f.Consume(challenge));
    }

    private sealed class Fixture : ILoginAccounts, IVerificationDelivery, IDisposable
    {
        public Guid Person { get; } = Guid.NewGuid();
        public Guid Organization { get; } = Guid.NewGuid();
        public Guid Session { get; } = Guid.NewGuid();
        public string[] Permissions = ["procedures.manage"];
        public string Code = "";
        public OperationVerification Service { get; }
        private readonly ActiveSessions sessions = new(TimeProvider.System);
        public Fixture() { sessions.Add(Session, Person); Service = new(this, this, sessions, TimeProvider.System); }
        public Task<LoginAccount?> GetActiveAsync(Guid person, CancellationToken ct) => Task.FromResult<LoginAccount?>(
            person == Person ? new(Person, Organization, "Test", "***", VerificationChannel.InstitutionalEmail, Permissions) : null);
        public Task<LoginAccount?> ValidatePasswordAsync(string identification, string password, CancellationToken ct) => Task.FromResult<LoginAccount?>(null);
        public Task SendAsync(Guid person, VerificationChannel channel, string code, CancellationToken ct) { Code = code; return Task.CompletedTask; }
        public Task<LoginChallenge?> Issue() => Service.BeginAsync(Person, Organization, Session, "procedures.manage", "hash", default);
        public Task<bool> Consume(LoginChallenge challenge, string hash = "hash") => Service.ConsumeAsync(Person, Organization, Session, "procedures.manage", hash, challenge.Id, Code, default);
        public void Dispose() => Service.Dispose();
    }
}
