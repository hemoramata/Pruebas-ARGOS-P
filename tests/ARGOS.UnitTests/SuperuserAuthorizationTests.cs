using ARGOS.Application.Security;

namespace ARGOS.UnitTests;

public sealed class SuperuserAuthorizationTests
{
    [Fact]
    public async Task CompletionRejectsAlteredAuthorizationFieldsOrExtendedExpiry()
    {
        var delivery = new Delivery();
        using var service = new SuperuserAuthorizationService(delivery, TimeProvider.System);
        var superuser = SuperuserAccount.Provision(Guid.NewGuid(), Guid.NewGuid(), "su", "Creator", "creator@example.net", "ref");
        var authority = InstitutionalAuthority.Register(superuser.OrganizationId, "Authority", "authority@example.net", "auth-ref");
        var session = Guid.NewGuid();
        var issued = (await service.BeginAsync(superuser, authority, session, "read", "CaseFile", "case-1"))!;
        Assert.False(service.Complete(superuser, authority, session, issued with { SubjectReference = "case-2" }, delivery.Code));
        Assert.False(service.Complete(superuser, authority, session, issued with { ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) }, delivery.Code));
        Assert.True(service.Complete(superuser, authority, session, issued, delivery.Code));
    }
    [Fact]
    public async Task EveryActionRequiresAuthorityEmailAndIsSingleUse()
    {
        var delivery = new Delivery();
        using var service = new SuperuserAuthorizationService(delivery, TimeProvider.System);
        var superuser = SuperuserAccount.Provision(Guid.NewGuid(), Guid.NewGuid(), "architect-1", "Architect", "superuser@institution.invalid", "email-challenge:su");
        var authority = InstitutionalAuthority.Register(superuser.OrganizationId, "Authority", "authority@institution.invalid", "verified:1");
        var session = Guid.NewGuid();
        var authorization = (await service.BeginAsync(superuser, authority, session, "read-case", "CaseFile", "case-1"))!;
        Assert.Equal(authority.Email, delivery.Destination);
        Assert.True(service.Complete(superuser, authority, session, authorization, delivery.Code));
        Assert.False(service.Complete(superuser, authority, session, authorization, delivery.Code));
    }

    [Fact]
    public async Task DisabledAuthorityOrSuperuserCannotAuthorize()
    {
        var delivery = new Delivery();
        using var service = new SuperuserAuthorizationService(delivery, TimeProvider.System);
        var superuser = SuperuserAccount.Provision(Guid.NewGuid(), Guid.NewGuid(), "architect-1", "Architect", "superuser@institution.invalid", "email-challenge:su");
        var authority = InstitutionalAuthority.Register(superuser.OrganizationId, "Authority", "authority@institution.invalid", "verified:1");
        authority.Disable();
        Assert.Null(await service.BeginAsync(superuser, authority, Guid.NewGuid(), "read", "CaseFile", "case-1"));
        authority = InstitutionalAuthority.Register(superuser.OrganizationId, "Authority", "authority@institution.invalid", "verified:2");
        superuser.Disable();
        Assert.Null(await service.BeginAsync(superuser, authority, Guid.NewGuid(), "read", "CaseFile", "case-1"));
    }

    [Fact]
    public async Task DifferentOrganizationCannotAuthorize()
    {
        using var service = new SuperuserAuthorizationService(new Delivery(), TimeProvider.System);
        var superuser = SuperuserAccount.Provision(Guid.NewGuid(), Guid.NewGuid(), "architect-1", "Architect", "superuser@institution.invalid", "email-challenge:su");
        var authority = InstitutionalAuthority.Register(Guid.NewGuid(), "Authority", "authority@institution.invalid", "verified:1");
        Assert.Null(await service.BeginAsync(superuser, authority, Guid.NewGuid(), "read", "CaseFile", "case-1"));
    }

    [Fact]
    public async Task ReissueInvalidatesPreviousAuthorizationAndRateLimits()
    {
        var clock = new TestClock(); var delivery = new Delivery();
        using var service = new SuperuserAuthorizationService(delivery, clock);
        var superuser = SuperuserAccount.Provision(Guid.NewGuid(), Guid.NewGuid(), "architect-1", "Architect", "superuser@institution.invalid", "email-challenge:su");
        var authority = InstitutionalAuthority.Register(superuser.OrganizationId, "Authority", "authority@institution.invalid", "verified:1");
        var firstSession = Guid.NewGuid();
        var first = (await service.BeginAsync(superuser, authority, firstSession, "read", "CaseFile", "1"))!;
        Assert.Null(await service.BeginAsync(superuser, authority, Guid.NewGuid(), "write", "CaseFile", "1"));
        clock.Advance(TimeSpan.FromMinutes(1));
        var secondSession = Guid.NewGuid();
        var second = (await service.BeginAsync(superuser, authority, secondSession, "write", "CaseFile", "1"))!;
        Assert.False(service.Complete(superuser, authority, firstSession, first, delivery.OldCode));
        Assert.True(service.Complete(superuser, authority, secondSession, second, delivery.Code));
    }

    private sealed class Delivery : IAuthorityApprovalDelivery
    {
        public string Destination = ""; public string Code = ""; public string OldCode = "";
        public Task SendAsync(string destination, string code, CancellationToken ct)
        { Destination = destination; OldCode = Code; Code = code; return Task.CompletedTask; }
    }
    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan amount) => now += amount;
    }
}
