using ARGOS.Application.ProcessEngine.Evaluation;
using ARGOS.Application.ProcessEngine.Execution;
using ARGOS.Application.ProcessEngine.Resolution;
using ARGOS.Domain.Aggregates.Organization.ValueObjects;
using ARGOS.Domain.Aggregates.Procedure.ValueObjects;
using ARGOS.Domain.Aggregates.Workflow;
using ARGOS.Domain.Aggregates.Workflow.Activities;
using ARGOS.Domain.Aggregates.Workflow.Conditions;
using ARGOS.Domain.Aggregates.Workflow.Transitions;

namespace ARGOS.UnitTests;

public sealed class TransitionResolverTests
{
    private readonly WorkflowActivity _start = WorkflowActivity.Create("A", "Inicio", "Inicio", 1);
    private readonly WorkflowActivity _end = WorkflowActivity.Create("B", "Fin", "Fin", 2);
    private readonly TransitionResolver _resolver = new(new ConditionEvaluator());

    private ProcessExecutionContext Context() => new(
        Workflow.Create(ProcedureId.New(), OrganizationId.New(), "TEST", "Prueba", "Prueba"), _start, Guid.NewGuid());

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OnlySatisfiedRouteIsReturnedWithoutMutatingContext(bool ready)
    {
        var context = Context();
        var route = WorkflowTransition.Create(_start.Id, _end.Id, "Continuar");
        context.ConfigureRouting([_start, _end], [route], [WorkflowCondition.Create(route.Id, "Ready == true", 1)]);
        context.SetItem("Ready", ready);
        Assert.Equal(ready ? _end : null, await _resolver.ResolveAsync(context));
        Assert.Same(_start, context.CurrentActivity);
    }

    [Fact]
    public async Task AllConditionsMustPass()
    {
        var context = Context();
        var route = WorkflowTransition.Create(_start.Id, _end.Id, "Continuar");
        context.ConfigureRouting([_start, _end], [route],
            [WorkflowCondition.Create(route.Id, "Ready == true", 2), WorkflowCondition.Create(route.Id, "Approved == true", 1)]);
        context.SetItem("Approved", false);
        // Ready is absent: evaluation must stop after the first false condition.
        Assert.Null(await _resolver.ResolveAsync(context));
    }

    [Fact]
    public async Task TerminalActivityHasNoNextActivity()
    {
        var context = Context();
        context.ConfigureRouting([_start], [], []);
        Assert.Null(await _resolver.ResolveAsync(context));
    }

    [Fact]
    public async Task MissingConfigurationIsNotMistakenForTerminalActivity()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _resolver.ResolveAsync(Context()));
    }

    [Fact]
    public async Task AmbiguousRoutesAreRejected()
    {
        var context = Context();
        context.ConfigureRouting([_start, _end],
            [WorkflowTransition.Create(_start.Id, _end.Id, "Uno"), WorkflowTransition.Create(_start.Id, _end.Id, "Dos")], []);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _resolver.ResolveAsync(context));
        Assert.Same(_start, context.CurrentActivity);
    }

    [Fact]
    public void InvalidReplacementPreservesPreviousConfiguration()
    {
        var context = Context();
        context.ConfigureRouting([_start], [], []);
        Assert.Throws<ArgumentException>(() => context.ConfigureRouting([_start],
            [WorkflowTransition.Create(_start.Id, _end.Id, "Destino ausente")], []));
        Assert.Empty(context.Transitions);
        Assert.True(context.RoutingConfigured);
    }

    [Fact]
    public async Task UnconditionalRouteAndCancellationAreHandled()
    {
        var context = Context();
        context.ConfigureRouting([_start, _end], [WorkflowTransition.Create(_start.Id, _end.Id, "Continuar")], []);
        Assert.Same(_end, await _resolver.ResolveAsync(context));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _resolver.ResolveAsync(context, new CancellationToken(true)));
    }
}
