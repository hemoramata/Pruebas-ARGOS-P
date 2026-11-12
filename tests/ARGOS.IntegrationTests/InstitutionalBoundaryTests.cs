using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ARGOS.Domain.Aggregates.CaseFile;
using ARGOS.Domain.Aggregates.CaseFile.ValueObjects;
using ARGOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ARGOS.IntegrationTests;

public sealed partial class LoginEndpointsTests
{
    [Fact]
    public async Task UploadedHtmlIsDownloadedAsAttachmentAndClosedCaseRejectsUploads()
    {
        await using var f = await CaseFixture.Start(new TestContentProvider());
        f.Host.Adapter.Permissions = ["case-files.read", "case-files.update"];
        await f.Login();
        var upload = new { ContentType = "text/html", Base64Content = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("<script>alert(1)</script>")) };
        var response = await f.Host.Client.PostAsJsonAsync(f.Path + "/content", upload);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        using var downloaded = await f.Host.Client.GetAsync(f.Path + "/content/" + content.GetProperty("id").GetGuid());
        downloaded.EnsureSuccessStatusCode();
        Assert.Equal("attachment", downloaded.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal("nosniff", downloaded.Headers.GetValues("X-Content-Type-Options").Single());
        await using (var db = f.Db())
        {
            (await db.CaseFiles.SingleAsync()).FinalizeWithResult("DONE");
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Conflict, (await f.Host.Client.PostAsJsonAsync(f.Path + "/content", upload)).StatusCode);
        await using var check = f.Db();
        Assert.Single(await check.ProtectedCaseContents.ToListAsync());
    }

    // Test transport only, deliberately not a cryptographic implementation.
    private sealed class TestContentProvider : ARGOS.Application.Security.IEnvelopeEncryptionProvider
    {
        public Task<ARGOS.Application.Security.EncryptedEnvelope> EncryptAsync(ReadOnlyMemory<byte> plaintext, string associatedData, CancellationToken ct = default)
            => Task.FromResult(new ARGOS.Application.Security.EncryptedEnvelope(Convert.ToBase64String(plaintext.Span), "test", "test", associatedData));
        public Task<byte[]> DecryptAsync(ARGOS.Application.Security.EncryptedEnvelope envelope, CancellationToken ct = default)
            => Task.FromResult(Convert.FromBase64String(envelope.Ciphertext));
    }
    [Fact]
    public async Task PublishedSetupRequiresInstitutionalAdministrator()
    {
        await using var f = await CaseFixture.Start();
        var created = await f.Host.Client.PostAsJsonAsync("/api/organization-setups/", new {
            OrganizationName = "Test institution", OrganizationCode = "TEST", Description = "Security fixture",
            Action = new { ActorReference = "local-installer", PolicyVersion = "v1", AuthorizationReference = "test-authorization" }
        });
        created.EnsureSuccessStatusCode();
        await using (var db = f.Db())
            await db.Database.ExecuteSqlRawAsync("UPDATE \"OrganizationSetups\" SET \"Status\" = 'Published'");
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Host.Client.GetAsync("/api/organization-setups/current")).StatusCode);
        await f.Login();
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Host.Client.GetAsync("/api/organization-setups/current")).StatusCode);
    }
    [Fact]
    public async Task ForeignCaseCannotBeListedReadChangedOrVerified()
    {
        await using var f = await CaseFixture.Start();
        f.Host.Adapter.Permissions = ["case-files.read", "case-files.update", "case-files.finalize"];
        var foreignId = Guid.NewGuid();
        await using (var db = f.Db())
        {
            var item = new CaseFile(foreignId, CaseFileNumber.Create("TT-01-0002-2026").Value);
            item.StartProcessing();
            db.Add(item);
            db.Add(AuditEntry.Create(Guid.NewGuid(), "foreign", "session", "CaseFile.Created", "CaseFile", foreignId.ToString(), "Succeeded", "fixture"));
            await db.SaveChangesAsync();
        }
        await f.Login();
        var list = await f.Host.Client.GetFromJsonAsync<JsonElement>("/api/case-files/");
        Assert.Single(list.EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await f.Host.Client.GetAsync($"/api/case-files/{foreignId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await f.Host.Client.PostAsJsonAsync($"/api/case-files/{foreignId}/hold", new { Reason = "unauthorized" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await f.Host.Client.PostAsJsonAsync($"/api/case-files/{foreignId}/finalize/verification", new { ResultCode = "APPROVED" })).StatusCode);
        await using var check = f.Db();
        Assert.False(await check.AuditEntries.AnyAsync(x => x.SubjectReference == foreignId.ToString() && x.ActionCode != "CaseFile.Created"));
    }

    [Fact]
    public async Task ForeignEvidenceCustodianCannotBeChosenByClient()
    {
        await using var f = await CaseFixture.Start();
        f.Host.Adapter.Permissions = ["evidences.create"];
        await f.Login();
        var response = await f.Host.Client.PostAsJsonAsync("/api/evidences/", new { CustodianOrganizationId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var db = f.Db();
        Assert.Empty(await db.Evidences.ToListAsync());
    }

    [Fact]
    public async Task ProcedureAdministrationRejectsAnonymousAndUnprivilegedUsers()
    {
        await using var f = await CaseFixture.Start();
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Host.Client.GetAsync("/api/procedures/")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Host.Client.PostAsJsonAsync("/api/procedures/", new { Code = "X", Name = "X", Description = "X" })).StatusCode);
        await f.Login();
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Host.Client.GetAsync("/api/procedures/")).StatusCode);
        f.Host.Adapter.Permissions = ["procedures.manage"];
        Assert.Equal(HttpStatusCode.OK, (await f.Host.Client.GetAsync("/api/procedures/")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await f.Host.Client.PostAsJsonAsync($"/api/procedures/{Guid.NewGuid()}/approve", new {
            ActorReference = "another-person", AssignmentReference = "fake", PositionReference = "fake",
            CompetenceCode = "fake", PolicyVersion = "v1", AuthorizationReference = "fake"
        })).StatusCode);
    }
}
