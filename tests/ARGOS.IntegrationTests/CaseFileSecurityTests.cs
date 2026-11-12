using System.Net;
using System.Net.Http.Json;
using ARGOS.Application.Security;
using ARGOS.Domain.Aggregates.CaseFile;
using ARGOS.Domain.Aggregates.CaseFile.Enums;
using ARGOS.Domain.Aggregates.CaseFile.ValueObjects;
using ARGOS.Infrastructure.Persistence;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ARGOS.IntegrationTests;

public sealed partial class LoginEndpointsTests
{
    [Fact]
    public async Task CaseEndpointsRejectAnonymousAndMissingPermissions()
    {
        await using var f = await CaseFixture.Start();
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Host.Client.GetAsync("/api/case-files/")).StatusCode);
        await f.Login();
        f.Host.Adapter.Permitted = false;
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Host.Client.GetAsync("/api/case-files/")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Issue("finalize")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Host.Client.PostAsJsonAsync($"/api/case-files/{Guid.NewGuid()}/finalize/verification", new { ResultCode = "APPROVED" })).StatusCode);
    }

    [Fact]
    public async Task FinalizeAndArchiveRequireSeparateFreshCodesAndPersistAudit()
    {
        await using var f = await CaseFixture.Start();
        await f.Login();
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Finalize(Guid.Empty, "")).StatusCode);
        using var issued = await f.Issue("finalize");
        issued.EnsureSuccessStatusCode();
        var c = (await issued.Content.ReadFromJsonAsync<LoginChallenge>())!;
        var code = f.Host.Adapter.Code;
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Finalize(c.Id, code, "DENIED")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await f.Finalize(c.Id, code)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Host.Client.PostAsJsonAsync(f.Path + "/archive", new { ChallengeId = c.Id, Code = code })).StatusCode);
        f.Clock.Advance(TimeSpan.FromMinutes(1));
        using var archiveIssued = await f.Issue("archive");
        archiveIssued.EnsureSuccessStatusCode();
        var archive = (await archiveIssued.Content.ReadFromJsonAsync<LoginChallenge>())!;
        Assert.Equal(HttpStatusCode.OK, (await f.Host.Client.PostAsJsonAsync(f.Path + "/archive", new { ChallengeId = archive.Id, Code = f.Host.Adapter.Code })).StatusCode);
        await using var db = f.Db();
        Assert.Equal(CaseFileStatus.Archived, (await db.CaseFiles.SingleAsync()).Status);
        var audit = await db.AuditEntries.ToListAsync();
        foreach (var action in new[] { "CaseFile.Finalized", "CaseFile.Archived" })
        {
            var execution = Assert.Single(audit, x => x.ActionCode == action);
            Assert.Contains(audit, x => x.ActionCode == "CaseFile.VerificationChecked" && x.Outcome == "Succeeded" && x.RequestReference == execution.RequestReference);
            Assert.NotEqual("anonymous", execution.ActorReference);
        }
        Assert.Contains(audit, x => x.Outcome == "Rejected");
        Assert.DoesNotContain(audit, x => (x.Details ?? "").Contains("Code ="));
    }

    [Fact]
    public async Task ProviderFailureIsAuditedWithoutChangingCase()
    {
        await using var f = await CaseFixture.Start();
        await f.Login();
        f.Host.Adapter.FailDelivery = true;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await f.Issue("finalize")).StatusCode);
        await using var db = f.Db();
        Assert.Equal(CaseFileStatus.InProgress, (await db.CaseFiles.SingleAsync()).Status);
        Assert.Single(await db.AuditEntries.Where(x => x.ActionCode == "CaseFile.VerificationDeliveryFailed").ToListAsync());
    }

    [Fact]
    public async Task DatabaseRejectsDeletionOfCaseFilesAndAuditEntries()
    {
        await using var f = await CaseFixture.Start();
        await using var db = f.Db();
        db.CaseFiles.Remove(await db.CaseFiles.SingleAsync());
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        await using var check = f.Db();
        Assert.True(await check.CaseFiles.AnyAsync());
        check.AuditEntries.Remove(await check.AuditEntries.FirstAsync());
        await Assert.ThrowsAsync<DbUpdateException>(() => check.SaveChangesAsync());
    }

    [Fact]
    public async Task AuditWriteFailureRollsBackCaseAndSuccessfulVerificationTogether()
    {
        await using var f = await CaseFixture.Start();
        await f.Login();
        using var issued = await f.Issue("finalize");
        var c = (await issued.Content.ReadFromJsonAsync<LoginChallenge>())!;
        await using (var db = f.Db())
            await db.Database.ExecuteSqlRawAsync("""
                CREATE FUNCTION reject_final_audit() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'simulated audit failure'; END; $$;
                CREATE TRIGGER reject_final_audit BEFORE INSERT ON "AuditEntries"
                FOR EACH ROW WHEN (NEW."ActionCode" = 'CaseFile.Finalized') EXECUTE FUNCTION reject_final_audit();
                """);
        Assert.Equal(HttpStatusCode.InternalServerError, (await f.Finalize(c.Id, f.Host.Adapter.Code)).StatusCode);
        await using (var db = f.Db())
        {
            Assert.Equal(CaseFileStatus.InProgress, (await db.CaseFiles.SingleAsync()).Status);
            Assert.False(await db.AuditEntries.AnyAsync(x => x.ActionCode == "CaseFile.VerificationChecked" && x.Outcome == "Succeeded"));
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER reject_final_audit ON \"AuditEntries\";");
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Finalize(c.Id, f.Host.Adapter.Code)).StatusCode);
    }

    [Fact]
    public async Task StaleCaseChangeCannotOverwriteAConcurrentFinalization()
    {
        await using var f = await CaseFixture.Start();
        await using var first = f.Db();
        await using var second = f.Db();
        var a = await first.CaseFiles.SingleAsync();
        var b = await second.CaseFiles.SingleAsync();
        a.FinalizeWithResult("APPROVED");
        await first.SaveChangesAsync();
        b.PutOnHold("Stale request");
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        await using var check = f.Db();
        Assert.Equal(CaseFileStatus.Finalized, (await check.CaseFiles.SingleAsync()).Status);
    }

    private sealed class CaseFixture(Host host, TestPostgresDatabase keeper, SessionClock clock, Guid id) : IAsyncDisposable
    {
        public Host Host => host;
        public SessionClock Clock => clock;
        public string Path => $"/api/case-files/{id}";
        public ArgosDbContext Db() => new(new DbContextOptionsBuilder<ArgosDbContext>().UseNpgsql(keeper.ConnectionString).Options);
        public static async Task<CaseFixture> Start(IEnvelopeEncryptionProvider? encryption = null)
        {
            var keeper = new TestPostgresDatabase();
            await keeper.OpenAsync();
            var clock = new SessionClock();
            var host = await Host.Start(true, false, clock, keeper.ConnectionString, encryption);
            host.Adapter.Permissions = ["case-files.read", "case-files.finalize", "case-files.archive"];
            var id = Guid.NewGuid();
            var f = new CaseFixture(host, keeper, clock, id);
            await using var db = f.Db();
            await TestPostgresDatabase.InitializeAsync(keeper.ConnectionString);
            var item = new CaseFile(id, CaseFileNumber.Create("TT-01-0001-2026").Value);
            item.StartProcessing();
            db.CaseFiles.Add(item);
            db.AuditEntries.Add(AuditEntry.Create(host.Adapter.OrganizationId, "test-fixture", null, "CaseFile.Created", "CaseFile", id.ToString(), "Succeeded", "fixture"));
            await db.SaveChangesAsync();
            return f;
        }
        public async Task Login()
        {
            using var begin = await host.Client.PostAsJsonAsync("/api/session/begin", new { Identification = "test", Password = "test" });
            var c = (await begin.Content.ReadFromJsonAsync<LoginChallenge>())!;
            using var verified = await host.Client.PostAsJsonAsync("/api/session/verify", new { c.Id, c.Binding, Code = host.Adapter.Code });
            verified.EnsureSuccessStatusCode();
            host.Client.DefaultRequestHeaders.Add("Cookie", verified.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith("ARGOS.Session=")).Split(';')[0]);
        }
        public Task<HttpResponseMessage> Issue(string action) => host.Client.PostAsJsonAsync(Path + "/" + action + "/verification", new { ResultCode = "APPROVED" });
        public Task<HttpResponseMessage> Finalize(Guid challenge, string code, string result = "APPROVED") => host.Client.PostAsJsonAsync(Path + "/finalize", new { ResultCode = result, ChallengeId = challenge, Code = code });
        public async ValueTask DisposeAsync() { await host.DisposeAsync(); await keeper.DisposeAsync(); }
    }
}
