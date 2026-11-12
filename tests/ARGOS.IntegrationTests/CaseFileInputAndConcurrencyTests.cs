using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ARGOS.Domain.Aggregates.Evidence;
using Microsoft.EntityFrameworkCore;

namespace ARGOS.IntegrationTests;

public sealed partial class LoginEndpointsTests
{
    [Fact]
    public async Task EvidenceLinkWaitsForConcurrentFinalizationAndThenRejectsIt()
    {
        await using var f = await CaseFixture.Start();
        f.Host.Adapter.Permissions = ["case-files.update"];
        await f.Login();
        var now = DateTimeOffset.UtcNow;
        var source = EvidenceSource.Create("PERSON", "contributor", "Author", now, now, "WEB").Value;
        var evidence = Evidence.Create(f.Host.Adapter.OrganizationId, "DOCUMENT", "PERSON", "test", source, now).Value;
        await using (var seed = f.Db()) { seed.Evidences.Add(evidence); await seed.SaveChangesAsync(); }
        await using var closing = f.Db();
        await using var transaction = await closing.Database.BeginTransactionAsync();
        var item = await closing.CaseFiles.FromSqlRaw("SELECT * FROM \"CaseFiles\" FOR UPDATE").SingleAsync();
        var pending = f.Host.Client.PostAsJsonAsync(f.Path + "/evidence", new {
            EvidenceId = evidence.Id.Value, RequirementCode = "REQ", ProcedureVersionId = Guid.NewGuid(),
            PolicyVersion = "v1", LinkedByPositionId = Guid.NewGuid(), ValidatedAtUtc = now, LinkedAtUtc = now });
        // Observe an actual PostgreSQL lock wait, not a timing assumption about request arrival.
        var waiting = false;
        for (var attempt = 0; attempt < 100 && !waiting; attempt++)
        {
            await using var observer = f.Db();
            waiting = await observer.Database.SqlQueryRaw<bool>("""
                SELECT EXISTS (SELECT 1 FROM pg_stat_activity
                WHERE datname = current_database() AND wait_event_type = 'Lock'
                AND query LIKE '%CaseFiles%FOR UPDATE%') AS "Value"
                """).SingleAsync();
            if (!waiting) await Task.Delay(20);
        }
        if (!waiting)
        {
            await transaction.RollbackAsync();
            using var ignored = await pending;
            Assert.True(waiting, "Evidence linking must lock the case before checking its status.");
            return;
        }
        Assert.True(item.FinalizeWithResult("DONE").IsSuccess);
        await closing.SaveChangesAsync();
        await transaction.CommitAsync();
        using var response = await pending;
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await using var check = f.Db();
        Assert.Empty((await check.CaseFiles.Include(x => x.EvidenceLinks).SingleAsync()).EvidenceLinks);
        Assert.False(await check.AuditEntries.AnyAsync(x => x.ActionCode == "Evidence.LinkedToCaseFile"));
    }

    [Fact]
    public async Task EmptyEvidenceIdAndOversizedHoldDoNotBecomeServerErrors()
    {
        await using var f = await CaseFixture.Start();
        f.Host.Adapter.Permissions = ["case-files.update"];
        await f.Login();
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.PostAsJsonAsync(f.Path + "/evidence",
            new { EvidenceId = Guid.Empty })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await f.Host.Client.PostAsJsonAsync(f.Path + "/hold",
            new { Reason = new string('x', 2001) })).StatusCode);
        await using var db = f.Db();
        Assert.Null((await db.CaseFiles.SingleAsync()).WaitingReason);
        Assert.False(await db.AuditEntries.AnyAsync(x => x.ActionCode == "CaseFile.PutOnHold"));
    }

    [Fact]
    public async Task ConcurrentDuplicateCaseCreationProducesOneRecordAndOneConflict()
    {
        await using var f = await CaseFixture.Start();
        f.Host.Adapter.Permissions = ["case-files.create"];
        await f.Login();
        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
            f.Host.Client.PostAsJsonAsync("/api/case-files/", new { Number = "TT-01-0002-2026" })));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        await using var db = f.Db();
        Assert.Equal(2, await db.CaseFiles.CountAsync());
        Assert.Equal(2, await db.AuditEntries.CountAsync(x => x.ActionCode == "CaseFile.Created"));
    }

    [Fact]
    public async Task ConcurrentEvidenceLinksAreSerializedAndAuditedOnlyOnce()
    {
        await using var f = await CaseFixture.Start(new TestContentProvider());
        var authority = await SeedAuthority(f, publishedProcedure: true);
        f.Host.Adapter.Permissions = ["case-files.update"];
        await f.Login();
        var now = f.Clock.GetUtcNow();
        var source = EvidenceSource.Create("PERSON", "contributor", "Author", now, now, "WEB").Value;
        var evidence = Evidence.Create(f.Host.Adapter.OrganizationId, "DOCUMENT", "PERSON", "test", source, now, authority.Proof).Value;
        await using (var seed = f.Db()) { seed.Evidences.Add(evidence); await seed.SaveChangesAsync(); }
        var request = new { EvidenceId = evidence.Id.Value, RequirementCode = "REQ", ProcedureVersionId = authority.Procedure,
            AssignmentId = authority.Assignment, CompetenceCode = "APPROVAL",
            PolicyVersion = "spoofed", LinkedByPositionId = Guid.NewGuid(), ValidatedAtUtc = now, LinkedAtUtc = now };
        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
            f.Host.Client.PostAsJsonAsync(f.Path + "/evidence", request)));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        await using var db = f.Db();
        Assert.Single((await db.CaseFiles.Include(x => x.EvidenceLinks).SingleAsync()).EvidenceLinks);
        Assert.Single(await db.AuditEntries.Where(x => x.ActionCode == "Evidence.LinkedToCaseFile").ToListAsync());
    }
}
