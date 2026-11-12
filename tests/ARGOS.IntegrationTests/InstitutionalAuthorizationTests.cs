using System.Net;
using System.Net.Http.Json;
using ARGOS.API.Endpoints;
using ARGOS.Application.Security;
using ARGOS.Domain.Aggregates.Organization;
using ARGOS.Domain.Aggregates.Organization.ValueObjects;
using ARGOS.Domain.Aggregates.OrganizationSetup;
using ARGOS.Domain.Aggregates.OrganizationSetup.Enums;
using ARGOS.Domain.Aggregates.Personnel;
using ARGOS.Domain.Aggregates.Procedure;
using ARGOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ARGOS.IntegrationTests;

public sealed partial class LoginEndpointsTests
{
    [Fact]
    public async Task OversizedDraftFieldsAreRejectedWithoutPersistingRecords()
    {
        await using var f = await CaseFixture.Start();
        await SeedAuthority(f);
        f.Host.Adapter.Permissions = ["procedures.manage"];
        await f.Login();
        foreach (var payload in new[] {
            new { Code = new string('X',51), Name = "Name", Description = "Description" },
            new { Code = "NEW", Name = new string('X',201), Description = "Description" },
            new { Code = "NEW", Name = "Name", Description = new string('X',2001) } })
            Assert.Equal(HttpStatusCode.BadRequest,(await f.Host.Client.PostAsJsonAsync("/api/procedures/",payload)).StatusCode);
        await using var db = f.Db();
        Assert.Equal(1,await db.Procedures.CountAsync());
    }

    [Fact]
    public async Task AdministrativeCatalogueIncludesOwnDraftButNotUnscopedRecords()
    {
        await using var f = await CaseFixture.Start();
        var a = await SeedAuthority(f);
        await using (var db = f.Db()) { db.Add(Procedure.Create("OTHER", "Other", "Not owned")); await db.SaveChangesAsync(); }
        f.Host.Adapter.Permissions = ["procedures.manage"];
        await f.Login();
        using var response = await f.Host.Client.GetAsync("/api/procedures/administration");
        response.EnsureSuccessStatusCode();
        var items = (await response.Content.ReadFromJsonAsync<ARGOS.Application.Modules.Procedures.ProcedureDto[]>())!;
        Assert.Equal(a.Procedure, Assert.Single(items).Id);
        Assert.Equal("Draft", items[0].Status);
        Assert.Empty((await f.Host.Client.GetFromJsonAsync<ARGOS.Application.Modules.Procedures.ProcedureDto[]>("/api/procedures/"))!);
    }

    [Fact]
    public async Task AuthorityOptionsExposeOnlyTheAuthenticatedPersonsAssignments()
    {
        await using var f = await CaseFixture.Start();
        var a = await SeedAuthority(f);
        f.Host.Adapter.Permissions = ["procedures.manage"];
        await f.Login();
        using var response = await f.Host.Client.GetAsync("/api/procedures/authority-options");
        response.EnsureSuccessStatusCode();
        using var body = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(f.Host.Adapter.PersonId, body.RootElement.GetProperty("personId").GetGuid());
        var assignment = Assert.Single(body.RootElement.GetProperty("assignments").EnumerateArray());
        Assert.Equal(a.Assignment, assignment.GetProperty("id").GetGuid());
        Assert.Equal(a.Position, assignment.GetProperty("positionId").GetGuid());
        Assert.Equal(a.Policy, Assert.Single(body.RootElement.GetProperty("policies").EnumerateArray()).GetProperty("id").GetGuid());
    }

    [Theory]
    [InlineData("subject")]
    [InlineData("expired")]
    [InlineData("type")]
    [InlineData("assignment")]
    [InlineData("content")]
    public async Task EvidenceUseRejectsUntrustedOrInapplicableContextWithoutRequestingMfa(string invalid)
    {
        await using var f = await CaseFixture.Start(new TestContentProvider());
        var a = await SeedAuthority(f, publishedProcedure: true);
        f.Host.Adapter.Permissions = ["case-files.update"];
        await f.Login();
        var now = f.Clock.GetUtcNow();
        var source = ARGOS.Domain.Aggregates.Evidence.EvidenceSource.Create("PERSON", "contributor", "Author",
            invalid == "expired" ? now.AddDays(-366) : now, now, "WEB").Value;
        var evidence = ARGOS.Domain.Aggregates.Evidence.Evidence.Create(f.Host.Adapter.OrganizationId,
            invalid == "type" ? "OTHER" : "DOCUMENT", "PERSON", invalid == "subject" ? "other-person" : "test",
            source, now, invalid == "content" ? null : a.Proof).Value;
        await using (var seed = f.Db()) { seed.Add(evidence); await seed.SaveChangesAsync(); }
        var loginCode = f.Host.Adapter.Code;
        using var response = await f.Host.Client.PostAsJsonAsync(f.Path + "/evidence", new {
            EvidenceId = evidence.Id.Value, RequirementCode = "REQ", ProcedureVersionId = a.Procedure,
            AssignmentId = invalid == "assignment" ? Guid.NewGuid() : a.Assignment, CompetenceCode = "APPROVAL" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(loginCode, f.Host.Adapter.Code);
        await using var check = f.Db();
        Assert.Empty((await check.CaseFiles.Include(x => x.EvidenceLinks).SingleAsync()).EvidenceLinks);
    }

    [Fact]
    public async Task DecisionAuditFailureRollsBackProcedureAndConsumesTheCode()
    {
        await using var f = await CaseFixture.Start();
        var a = await SeedAuthority(f);
        f.Host.Adapter.Permissions = ["procedures.manage"];
        await f.Login();
        var decision = DecisionFor(f, a);
        var path = $"/api/procedures/{a.Procedure}/submit-review";
        using var issued = await f.Host.Client.PostAsJsonAsync(path + "/verification", new { Decision = decision });
        issued.EnsureSuccessStatusCode();
        var challenge = (await issued.Content.ReadFromJsonAsync<LoginChallenge>())!;
        var request = new { Decision = decision with { ChallengeId = challenge.Id, Code = f.Host.Adapter.Code } };
        await using (var seed = f.Db())
            await seed.Database.ExecuteSqlRawAsync("""
                CREATE FUNCTION reject_decision_audit() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'simulated failure'; END; $$;
                CREATE TRIGGER reject_decision_audit BEFORE INSERT ON "AuditEntries"
                FOR EACH ROW WHEN (NEW."ActionCode"='Procedure.DecisionAuthorized') EXECUTE FUNCTION reject_decision_audit();
                """);
        Assert.Equal(HttpStatusCode.InternalServerError, (await f.Host.Client.PostAsJsonAsync(path, request)).StatusCode);
        await using (var check = f.Db())
        {
            Assert.Empty((await check.Procedures.SingleAsync()).Decisions);
            Assert.False(await check.AuditEntries.AnyAsync(x => x.ActionCode == "Procedure.DecisionAuthorized"));
            await check.Database.ExecuteSqlRawAsync("DROP TRIGGER reject_decision_audit ON \"AuditEntries\"");
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Host.Client.PostAsJsonAsync(path, request)).StatusCode);
    }

    private sealed record AuthorityFixture(Guid Assignment, Guid Position, Guid Policy, Guid Procedure, Guid Proof);
    private static async Task<AuthorityFixture> SeedAuthority(CaseFixture f, bool publishedProcedure = false)
    {
        await using var scope = f.Host.Services.CreateAsyncScope();
        var personnel = scope.ServiceProvider.GetRequiredService<PersonnelDbContext>();
        var access = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
        await using var db = f.Db();
        var organization = Organization.Create("Fixture", "FIXTURE");
        db.Add(organization);
        db.Entry(organization).Property(x => x.Id).CurrentValue = OrganizationId.From(f.Host.Adapter.OrganizationId);
        var context = OrganizationSetupActionContext.Create("fixture", "v1", "retained-fixture", DateTime.UtcNow).Value;
        var setup = OrganizationSetup.CreateInitial(organization.Id, context).Value;
        foreach (var concept in Enum.GetValues<OrganizationConcept>()) setup.SetTerm(concept, concept.ToString(), concept + "s");
        var unit = setup.AddUnit("UNIT", "Unit").Value;
        var position = setup.AddPosition("POSITION", "Position", unit).Value;
        var competence = setup.AddCompetence("APPROVAL", "Approval", "Fixture").Value;
        var capacity = setup.AddActingCapacity("CAPACITY", "Capacity").Value;
        setup.AssignCompetence(position, competence); setup.AssignActingCapacity(position, capacity); setup.Publish(null, context);
        db.Add(setup);
        var person = new InstitutionalPerson(organization.Id, new("Fixture", "ID", "001"), "Test");
        var assignment = person.Assign(setup, position, f.Clock.GetUtcNow().AddDays(-1), null, "retained-appointment");
        personnel.Add(person); await personnel.SaveChangesAsync();
        var designations = scope.ServiceProvider.GetRequiredService<DesignationDbContext>();
        designations.Add(AdministratorDesignation.Create(person, assignment.Id, "institutional-appointment", "institution", "fixture",
            f.Clock.GetUtcNow(), f.Clock.GetUtcNow().AddDays(-1), null));
        await designations.SaveChangesAsync();
        f.Host.Adapter.PersonId = person.Id;
        foreach (var permission in new[] { "procedures.manage", "personnel.manage", "case-files.update" })
            access.PositionPermissions.Add(new() { OrganizationId = organization.Id.Value, PositionId = position, Permission = permission });
        await access.SaveChangesAsync();
        var caseFile = await db.CaseFiles.SingleAsync();
        var proof = ProtectedCaseContent.Create(caseFile.Id, "text/plain", new("dGVzdA==", "test", "test", $"CaseFile:{caseFile.Id:D}"), f.Clock.GetUtcNow());
        db.Add(proof);
        var policy = InstitutionalOperationPolicy.Create(organization.Id.Value, 1, proof.Id, person.Id, assignment.Id, Guid.NewGuid(),
            new("P-SECURE", InstitutionalOperationPolicy.Actions.Select(action => new ActionAuthority(action, position, "APPROVAL")).ToArray(),
                1, true, false, [new("REQ", "DOCUMENT", "PERSON", 365, true)]), f.Clock.GetUtcNow());
        db.Add(policy);
        var procedure = Procedure.Create("P-SECURE", "Secure", "Secure fixture");
        if (publishedProcedure)
        {
            ProcedureDecisionContext Decision(string actor) => ProcedureDecisionContext.Create(actor, assignment.Id.ToString(), position.ToString(),
                "APPROVAL", policy.Id.ToString(), "fixture", null, DateTime.UtcNow).Value;
            Assert.True(procedure.SubmitForReview(1, true, false, false, Decision("writer")).IsSuccess);
            Assert.True(procedure.Approve(Decision("approver")).IsSuccess);
            Assert.True(procedure.Publish(null, Decision("publisher")).IsSuccess);
        }
        db.Add(procedure);
        db.AuditEntries.Add(AuditEntry.Create(organization.Id.Value, person.Id.ToString(), "fixture", "Procedure.Created",
            "Procedure", procedure.Id.Value.ToString(), "Succeeded", "fixture"));
        db.Entry(caseFile).Property(x => x.SubjectTypeCode).CurrentValue = "PERSON";
        db.Entry(caseFile).Property(x => x.SubjectReference).CurrentValue = "test";
        db.Entry(caseFile).Property(x => x.ProcedureVersionId).CurrentValue = procedure.Id.Value;
        await db.SaveChangesAsync();
        return new(assignment.Id, position, policy.Id, procedure.Id.Value, proof.Id);
    }

    private static ProcedureDecisionRequest DecisionFor(CaseFixture f, AuthorityFixture a) => new(f.Host.Adapter.PersonId.ToString(),
        a.Assignment.ToString(), "spoofed-position", "APPROVAL", "spoofed-policy", "spoofed-proof", "Test decision", a.Policy);

    [Fact]
    public async Task ProcedureDecisionRequiresFreshMfaAndPersistsServerAuthorityAtomically()
    {
        await using var f = await CaseFixture.Start();
        var a = await SeedAuthority(f);
        f.Host.Adapter.Permissions = ["procedures.manage"];
        await f.Login();
        var decision = DecisionFor(f, a);
        var path = $"/api/procedures/{a.Procedure}/submit-review";
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Host.Client.PostAsJsonAsync(path, new { Decision = decision })).StatusCode);
        using var issued = await f.Host.Client.PostAsJsonAsync(path + "/verification", new { Decision = decision });
        issued.EnsureSuccessStatusCode();
        var challenge = (await issued.Content.ReadFromJsonAsync<LoginChallenge>())!;
        var code = f.Host.Adapter.Code;
        var verified = decision with { ChallengeId = challenge.Id, Code = code };
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Host.Client.PostAsJsonAsync(path, new { Decision = verified with { Comments = "tampered" } })).StatusCode);
        using var response = await f.Host.Client.PostAsJsonAsync(path, new { Decision = verified,
            Policy = new { RequiredApprovalCount = 0, SeparationOfDutiesRequired = false } });
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Host.Client.PostAsJsonAsync(path, new { Decision = verified })).StatusCode);
        await using var db = f.Db();
        var procedure = await db.Procedures.SingleAsync();
        Assert.Equal(1, procedure.RequiredApprovalCount);
        Assert.True(procedure.SeparationOfDutiesRequired);
        Assert.Equal(a.Policy.ToString(), procedure.ApprovalPolicyVersion);
        var saved = Assert.Single(procedure.Decisions);
        Assert.Equal(a.Position.ToString(), saved.PositionReference);
        Assert.Contains(challenge.Id.ToString(), saved.AuthorizationReference);
        Assert.Single(await db.AuditEntries.Where(x => x.ActionCode == "Procedure.DecisionAuthorized").ToListAsync());
    }

    [Fact]
    public async Task RevokedAppointmentInvalidatesAnAlreadyIssuedDecisionCode()
    {
        await using var f = await CaseFixture.Start();
        var a = await SeedAuthority(f);
        f.Host.Adapter.Permissions = ["procedures.manage"];
        await f.Login();
        var decision = DecisionFor(f, a);
        var path = $"/api/procedures/{a.Procedure}/submit-review";
        using var issued = await f.Host.Client.PostAsJsonAsync(path + "/verification", new { Decision = decision });
        issued.EnsureSuccessStatusCode();
        var challenge = (await issued.Content.ReadFromJsonAsync<LoginChallenge>())!;
        await using (var scope = f.Host.Services.CreateAsyncScope())
        {
            var personnel = scope.ServiceProvider.GetRequiredService<PersonnelDbContext>();
            (await personnel.People.SingleAsync()).RevokeAssignment(a.Assignment, f.Clock.GetUtcNow(), "revocation");
            await personnel.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Host.Client.PostAsJsonAsync(path,
            new { Decision = decision with { ChallengeId = challenge.Id, Code = f.Host.Adapter.Code } })).StatusCode);
        await using var db = f.Db();
        Assert.Empty((await db.Procedures.SingleAsync()).Decisions);
    }

    [Fact]
    public async Task PolicyPublicationRequiresRetainedProofAndMfaAndCannotBeRewritten()
    {
        await using var f = await CaseFixture.Start();
        var a = await SeedAuthority(f);
        f.Host.Adapter.Permissions = ["personnel.manage"];
        await f.Login();
        await using var db = f.Db();
        var original = await db.OperationPolicies.SingleAsync();
        var request = new InstitutionalPolicyEndpoints.PublishPolicyRequest(a.Assignment, "APPROVAL", a.Proof, 1, original.Rules);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.PostAsJsonAsync("/api/institutional-policies/verification", request with { ProofContentId = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Host.Client.PostAsJsonAsync("/api/institutional-policies/", request)).StatusCode);
        using var issued = await f.Host.Client.PostAsJsonAsync("/api/institutional-policies/verification", request);
        issued.EnsureSuccessStatusCode();
        var challenge = (await issued.Content.ReadFromJsonAsync<LoginChallenge>())!;
        using var published = await f.Host.Client.PostAsJsonAsync("/api/institutional-policies/", request with { ChallengeId = challenge.Id, Code = f.Host.Adapter.Code });
        Assert.Equal(HttpStatusCode.Created, published.StatusCode);
        Assert.Equal(2, await db.OperationPolicies.CountAsync());
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlRawAsync("UPDATE \"InstitutionalOperationPolicies\" SET \"Version\"=99"));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlRawAsync("TRUNCATE \"InstitutionalOperationPolicies\""));
    }
}
