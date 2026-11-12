using ARGOS.Domain.Aggregates.CaseFile;
using ARGOS.Domain.Aggregates.CaseFile.ValueObjects;
using ARGOS.Domain.Aggregates.Organization.ValueObjects;
using ARGOS.Domain.Aggregates.Procedure;
using ARGOS.Domain.Aggregates.Workflow;
using ARGOS.Domain.Aggregates.Workflow.Activities;
using ARGOS.Domain.Aggregates.Workflow.Transitions;
using ARGOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ARGOS.IntegrationTests;

public sealed class CaseWorkflowRunTests
{
    [Fact]
    public async Task TerminalRouteDoesNotFinalizeCaseAndHistoryCannotBeErased()
    {
        var f = await Fixture.Create();
        var run = await f.Start();
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Start());
        await f.Service.AdvanceAsync(run.Id, 0, new Dictionary<string, bool>(), f.Actor);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.AdvanceAsync(run.Id, 1, new Dictionary<string, bool>(), f.Actor));
        await using var db = new ArgosDbContext(f.Options);
        Assert.Equal(ARGOS.Domain.Aggregates.CaseFile.Enums.CaseFileStatus.InProgress, (await db.CaseFiles.SingleAsync()).Status);
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM \"CaseWorkflowSteps\""));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM \"CaseWorkflowRuns\""));
        Assert.Equal(2, await db.Set<CaseWorkflowStep>().CountAsync());
    }
    [Fact]
    public async Task ConcurrentAdvanceHasOneWinnerAndOneHistoryStep()
    {
        var f = await Fixture.Create();
        var run = await f.Start();
        async Task<bool> Attempt()
        {
            try { await f.Service.AdvanceAsync(run.Id, 0, new Dictionary<string, bool>(), f.Actor); return true; }
            catch (DbUpdateConcurrencyException) { return false; }
        }
        var results = await Task.WhenAll(Attempt(), Attempt());
        Assert.Single(results, x => x);
        await using var db = new ArgosDbContext(f.Options);
        var stored = await db.Set<CaseWorkflowRun>().SingleAsync();
        Assert.Equal(1, stored.Revision);
        Assert.Equal(f.End.Id.Value, stored.CurrentActivityId);
        Assert.Equal(2, await db.Set<CaseWorkflowStep>().CountAsync());
        Assert.Equal(2, await db.AuditEntries.CountAsync(x => x.ActionCode.StartsWith("CaseWorkflow.")));
    }

    [Fact]
    public async Task AuditFailureRollsBackAdvanceAndHistory()
    {
        var f = await Fixture.Create();
        var run = await f.Start();
        await using var db = new ArgosDbContext(f.Options);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION reject_workflow_audit() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'test audit failure'; END; $$;
            CREATE TRIGGER reject_test_audit BEFORE INSERT ON "AuditEntries"
            FOR EACH ROW EXECUTE FUNCTION reject_workflow_audit();
            """);
        await Assert.ThrowsAsync<DbUpdateException>(() => f.Service.AdvanceAsync(run.Id, 0, new Dictionary<string, bool>(), f.Actor));
        var stored = await db.Set<CaseWorkflowRun>().SingleAsync();
        Assert.Equal(0, stored.Revision);
        Assert.Equal(f.Begin.Id.Value, stored.CurrentActivityId);
        Assert.Single(await db.Set<CaseWorkflowStep>().ToListAsync());
    }

    [Fact]
    public async Task WaitingCaseAndForeignInstitutionCannotAdvance()
    {
        var f = await Fixture.Create();
        var run = await f.Start();
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.AdvanceAsync(run.Id, 0,
            new Dictionary<string, bool>(), f.Actor with { OrganizationId = Guid.NewGuid() }));
        await using var db = new ArgosDbContext(f.Options);
        var caseFile = await db.CaseFiles.SingleAsync();
        Assert.True(caseFile.PutOnHold("Subsanación").IsSuccess);
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.AdvanceAsync(run.Id, 0, new Dictionary<string, bool>(), f.Actor));
        Assert.Single(await db.Set<CaseWorkflowStep>().ToListAsync());
    }

    private sealed class Fixture
    {
        public DbContextOptions<ArgosDbContext> Options { get; init; } = null!;
        public PostgresCaseWorkflowService Service => new(Options);
        public PostgresCaseWorkflowService.Actor Actor { get; init; } = null!;
        public Workflow Workflow { get; init; } = null!;
        public WorkflowActivity Begin { get; init; } = null!;
        public WorkflowActivity End { get; init; } = null!;
        public Guid CaseId { get; init; }
        public Task<CaseWorkflowRun> Start() => Service.StartAsync(CaseId, Workflow.Id, Begin.Id.Value, Actor);
        public static async Task<Fixture> Create()
        {
            var database = new TestPostgresDatabase();
            await TestPostgresDatabase.InitializeAsync(database.ConnectionString);
            var options = new DbContextOptionsBuilder<ArgosDbContext>().UseNpgsql(database.ConnectionString).Options;
            var organization = OrganizationId.New();
            var procedure = Procedure.Create("RUN", "Prueba", "Prueba");
            var workflow = Workflow.Create(procedure.Id, organization, "RUN", "Ruta", "Ruta");
            var begin = WorkflowActivity.Create("A", "A", "A", 1);
            var end = WorkflowActivity.Create("B", "B", "B", 2);
            workflow.AddActivity(begin);
            workflow.AddActivity(end);
            workflow.AddTransition(WorkflowTransition.Create(begin.Id, end.Id, "Avanzar"));
            workflow.Activate();
            var caseFile = new CaseFile(Guid.NewGuid(), CaseFileNumber.Create("US-DI-0001-2026").Value);
            Assert.True(caseFile.StartProcessing().IsSuccess);
            await using var db = new ArgosDbContext(options);
            db.Add(procedure);
            db.Add(workflow);
            db.Add(caseFile);
            db.Add(AuditEntry.Create(organization.Value, "tester", "session", "CaseFile.Created", "CaseFile", caseFile.Id.ToString(), "Succeeded", "fixture"));
            await db.SaveChangesAsync();
            // Fixture only: isolates routing from the independently tested publication process.
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Procedures\" SET \"Status\" = 'Published' WHERE \"Id\" = {procedure.Id.Value}");
            return new Fixture { Options = options, Actor = new(organization.Value, "tester", "session", "request"),
                Workflow = workflow, Begin = begin, End = end, CaseId = caseFile.Id };
        }
    }
}
