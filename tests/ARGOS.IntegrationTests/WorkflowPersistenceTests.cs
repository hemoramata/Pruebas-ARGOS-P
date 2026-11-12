using ARGOS.Application.ProcessEngine.Evaluation;
using ARGOS.Application.ProcessEngine.Resolution;
using ARGOS.Domain.Aggregates.Organization.ValueObjects;
using ARGOS.Domain.Aggregates.Procedure;
using ARGOS.Domain.Aggregates.Procedure.ValueObjects;
using ARGOS.Domain.Aggregates.Workflow;
using ARGOS.Domain.Aggregates.Workflow.Activities;
using ARGOS.Domain.Aggregates.Workflow.Conditions;
using ARGOS.Domain.Aggregates.Workflow.Transitions;
using ARGOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ARGOS.IntegrationTests;

public sealed class WorkflowPersistenceTests
{
    [Fact]
    public async Task ReloadedGraphResolvesConditionsAndRejectsForeignScope()
    {
        await using var database = new TestPostgresDatabase();
        await TestPostgresDatabase.InitializeAsync(database.ConnectionString);
        var options = new DbContextOptionsBuilder<ArgosDbContext>().UseNpgsql(database.ConnectionString).Options;
        var procedure = Procedure.Create("TEST-FLOW", "Prueba", "Flujo persistente");
        var organization = OrganizationId.New();
        var workflow = Workflow.Create(procedure.Id, organization, "FLOW", "Flujo", "Prueba");
        var start = WorkflowActivity.Create("START", "Inicio", "Inicio", 1);
        var end = WorkflowActivity.Create("END", "Fin", "Fin", 2);
        var route = WorkflowTransition.Create(start.Id, end.Id, "Continuar");
        workflow.AddActivity(start);
        workflow.AddActivity(end);
        workflow.AddTransition(route);
        workflow.AddCondition(WorkflowCondition.Create(route.Id, "Ready == true", 1));
        await using (var save = new ArgosDbContext(options))
        {
            save.Procedures.Add(procedure);
            save.Workflows.Add(workflow);
            await save.SaveChangesAsync();
        }
        await using var read = new ArgosDbContext(options);
        var loader = new PostgresWorkflowLoader(read);
        var context = await loader.LoadAsync(workflow.Id, organization, procedure.Id, start.Id, Guid.NewGuid());
        Assert.Equal(2, context.Activities.Count);
        Assert.Single(context.Conditions);
        context.SetItem("Ready", false);
        var resolver = new TransitionResolver(new ConditionEvaluator());
        Assert.Null(await resolver.ResolveAsync(context));
        context.SetItem("Ready", true);
        Assert.Equal(end.Id, (await resolver.ResolveAsync(context))!.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => loader.LoadAsync(workflow.Id,
            OrganizationId.New(), procedure.Id, start.Id, Guid.NewGuid()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => loader.LoadAsync(workflow.Id,
            organization, ProcedureId.New(), start.Id, Guid.NewGuid()));
    }
}
