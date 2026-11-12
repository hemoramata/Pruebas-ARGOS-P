using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ARGOS.Application.Security;
using ARGOS.Domain.Aggregates.Organization;
using ARGOS.Domain.Aggregates.OrganizationSetup;
using ARGOS.Domain.Aggregates.OrganizationSetup.Enums;
using ARGOS.Domain.Aggregates.Personnel;
using ARGOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ARGOS.IntegrationTests;

// Test-only transport and institutional fixtures. The real policy guard, resolver,
// database transactions and second-factor verification remain active.
internal sealed class ProcedureSecurityTestClient(IServiceProvider services, ProcedureTestDelivery delivery, ProcedureTestClock clock)
    : DelegatingHandler(new HttpClientHandler())
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (request.Content is null) return await base.SendAsync(request, ct);
        var body = JsonNode.Parse(await request.Content.ReadAsStringAsync(ct))!.AsObject();
        var decision = (body["decision"] ?? body) as JsonObject;
        var alias = decision?["actorReference"]?.GetValue<string>();
        if (alias is not null && string.IsNullOrWhiteSpace(alias)) { request.Headers.Add("X-Test-Actor", alias); return await base.SendAsync(request, ct); }
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ArgosDbContext>();
        var personnel = scope.ServiceProvider.GetRequiredService<PersonnelDbContext>();
        var access = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
        var setup = await db.OrganizationSetups.FirstOrDefaultAsync(ct);
        if (setup is null)
        {
            var organization = Organization.Create("Fixture", "FIXTURE");
            var context = OrganizationSetupActionContext.Create("fixture", "v1", "fixture-proof", DateTime.UtcNow).Value;
            setup = OrganizationSetup.CreateInitial(organization.Id, context).Value;
            foreach (var concept in Enum.GetValues<OrganizationConcept>()) setup.SetTerm(concept, concept.ToString(), concept + "s");
            var unit = setup.AddUnit("UNIT", "Unit").Value;
            var position = setup.AddPosition("POSITION", "Position", unit).Value;
            var competence = setup.AddCompetence("PROCEDURE-APPROVAL", "Approval", "Fixture").Value;
            var capacity = setup.AddActingCapacity("CAPACITY", "Capacity").Value;
            setup.AssignCompetence(position, competence); setup.AssignActingCapacity(position, capacity);
            setup.Publish(null, context);
            db.Add(organization); db.Add(setup); await db.SaveChangesAsync(ct);
            access.PositionPermissions.Add(new() { OrganizationId = organization.Id.Value, PositionId = position, Permission = "procedures.manage" });
            await access.SaveChangesAsync(ct);
        }
        if (alias is null) return await base.SendAsync(request, ct);
        var person = await personnel.People.SingleOrDefaultAsync(x => x.FullName == alias, ct);
        if (person is null)
        {
            person = new(setup.OrganizationId, new("Fixture", "ID", alias), alias);
            person.Assign(setup, setup.Positions.Single().Id, DateTimeOffset.UtcNow.AddDays(-1), null, "persisted-appointment");
            personnel.Add(person); await personnel.SaveChangesAsync(ct);
        }
        clock.Advance();
        var session = Guid.NewGuid();
        services.GetRequiredService<ActiveSessions>().Add(session, person.Id);
        request.Headers.Add("X-Test-Actor", person.Id.ToString());
        request.Headers.Add("X-Test-Session", session.ToString());
        decision!["actorReference"] = person.Id.ToString();
        decision["assignmentReference"] = person.Assignments.Single().Id.ToString();
        decision["positionReference"] = setup.Positions.Single().Id.ToString();
        var parts = request.RequestUri!.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var id = Guid.Parse(parts[2]);
        var procedure = await db.Procedures.SingleAsync(x => x.Id == ARGOS.Domain.Aggregates.Procedure.ValueObjects.ProcedureId.From(id), ct);
        InstitutionalOperationPolicy? policy = null;
        if (parts[^1] == "submit-review" && body["policy"] is JsonObject supplied && supplied["requiredApprovalCount"]!.GetValue<int>() > 0)
        {
            var proof = ProtectedCaseContent.Create(Guid.NewGuid(), "text/plain", new("fixture", "fixture", "fixture", "fixture"), clock.GetUtcNow());
            db.Add(proof);
            var version = (await db.OperationPolicies.Where(x => x.ProcedureCode == procedure.Code).Select(x => (int?)x.Version).MaxAsync(ct) ?? 0) + 1;
            var rules = new InstitutionalPolicyRules(procedure.Code,
                InstitutionalOperationPolicy.Actions.Select(action => new ActionAuthority(action, setup.Positions.Single().Id, "PROCEDURE-APPROVAL")).ToArray(),
                supplied["requiredApprovalCount"]!.GetValue<int>(), supplied["separationOfDutiesRequired"]!.GetValue<bool>(),
                supplied["emergencyPublicationAllowed"]!.GetValue<bool>(), []);
            policy = InstitutionalOperationPolicy.Create(setup.OrganizationId.Value, version, proof.Id, person.Id,
                person.Assignments.Single().Id, Guid.NewGuid(), rules, clock.GetUtcNow());
            db.Add(policy); await db.SaveChangesAsync(ct);
        }
        else if (Guid.TryParse(procedure.ApprovalPolicyVersion, out var policyId)) policy = await db.OperationPolicies.SingleAsync(x => x.Id == policyId, ct);
        if (policy is not null)
        {
            decision["policyId"] = policy.Id;
            var verifyBody = new JsonObject { ["decision"] = decision.DeepClone(), ["ratificationDueAtUtc"] = body["ratificationDueAtUtc"]?.DeepClone() };
            using var verify = new HttpRequestMessage(HttpMethod.Post, new Uri(request.RequestUri.AbsoluteUri.TrimEnd('/') + "/verification")) { Content = JsonContent.Create(verifyBody) };
            verify.Headers.Add("X-Test-Actor", person.Id.ToString()); verify.Headers.Add("X-Test-Session", session.ToString());
            using var challengeResponse = await base.SendAsync(verify, ct);
            Assert.True(challengeResponse.IsSuccessStatusCode, await challengeResponse.Content.ReadAsStringAsync(ct));
            var challenge = (await challengeResponse.Content.ReadFromJsonAsync<LoginChallenge>(ct))!;
            decision["challengeId"] = challenge.Id; decision["code"] = delivery.Code;
        }
        request.Content = JsonContent.Create(body);
        return await base.SendAsync(request, ct);
    }
}

internal sealed class ProcedureTestClock : TimeProvider
{
    private DateTimeOffset now = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => now;
    public void Advance() => now = now.AddMinutes(2);
}
internal sealed class ProcedureTestDelivery(IServiceScopeFactory scopes) : ILoginAccounts, IVerificationDelivery
{
    public string Code { get; private set; } = "";
    public Task SendAsync(Guid personId, VerificationChannel channel, string code, CancellationToken ct) { Code = code; return Task.CompletedTask; }
    public Task<LoginAccount?> ValidatePasswordAsync(string identification, string password, CancellationToken ct) => Task.FromResult<LoginAccount?>(null);
    public async Task<LoginAccount?> GetActiveAsync(Guid personId, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var person = await scope.ServiceProvider.GetRequiredService<PersonnelDbContext>().People.AsNoTracking().SingleOrDefaultAsync(x => x.Id == personId, ct);
        return person is null ? null : new(person.Id, person.OrganizationId.Value, person.FullName, "fixture", VerificationChannel.InstitutionalEmail, ["procedures.manage"]);
    }
}
