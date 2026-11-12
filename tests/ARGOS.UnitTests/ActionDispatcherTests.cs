using ARGOS.Application.ProcessEngine.Contracts;
using ARGOS.Application.ProcessEngine.Dispatcher;
using ARGOS.Application.ProcessEngine.Execution;
using ARGOS.Domain.Aggregates.Organization.ValueObjects;
using ARGOS.Domain.Aggregates.Procedure.ValueObjects;
using ARGOS.Domain.Aggregates.Workflow;
using ARGOS.Domain.Aggregates.Workflow.Actions;
using ARGOS.Domain.Aggregates.Workflow.Actions.Enums;
using ARGOS.Domain.Aggregates.Workflow.Actions.ValueObjects;
using ARGOS.Domain.Aggregates.Workflow.Activities;

namespace ARGOS.UnitTests;

public sealed class ActionDispatcherTests
{
    private readonly WorkflowActivity _current = WorkflowActivity.Create("A", "A", "A", 1);
    private readonly WorkflowActivity _other = WorkflowActivity.Create("B", "B", "B", 2);
    private ProcessExecutionContext Context()
    {
        var context = new ProcessExecutionContext(Workflow.Create(ProcedureId.New(), OrganizationId.New(), "F", "F", "F"), _current, Guid.NewGuid());
        context.ConfigureRouting([_current, _other], [], []);
        return context;
    }
    private WorkflowAction Action(string name, int order, WorkflowActionType type = WorkflowActionType.CreateTask,
        WorkflowExecutionMoment moment = WorkflowExecutionMoment.BeforeStart, WorkflowActivity? activity = null) =>
        WorkflowAction.Create((activity ?? _current).Id, name, type, moment, order, WorkflowActionConfiguration.Create([]));

    [Fact]
    public async Task DispatchesOnlyCurrentActivityAndMomentInOrder()
    {
        var context = Context();
        var disabled = Action("disabled", 3);
        disabled.Deactivate();
        context.ConfigureActions([Action("second", 2), Action("first", 1), disabled,
            Action("other", 1, activity: _other), Action("later", 1, moment: WorkflowExecutionMoment.AfterFinish)], WorkflowExecutionMoment.BeforeStart);
        var calls = new List<string>();
        var handler = new Handler(calls);
        var unrelated = new Handler(calls, WorkflowActionType.SendEmail);
        await new ActionDispatcher([handler, unrelated]).DispatchAsync(context);
        Assert.Equal(new[] { "first", "second" }, calls);
        Assert.Equal("second", context.GetItem<string>("LastActionName"));
        Assert.Null(context.CurrentAction);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrAmbiguousCapabilityRejectsEntirePlanBeforeSideEffects(bool duplicate)
    {
        var context = Context();
        context.ConfigureActions([Action("first", 1), Action("missing", 2, WorkflowActionType.SendEmail)], WorkflowExecutionMoment.BeforeStart);
        var calls = new List<string>();
        var handlers = new List<IActionCapability> { new Handler(calls) };
        if (duplicate)
        {
            handlers.Add(new Handler(calls, WorkflowActionType.SendEmail));
            handlers.Add(new Handler(calls, WorkflowActionType.SendEmail));
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ActionDispatcher(handlers).DispatchAsync(context));
        Assert.Empty(calls);
        Assert.Null(context.CurrentAction);
    }

    [Fact]
    public async Task UnconfiguredPlanIsRejectedButExplicitEmptyPlanDoesNothing()
    {
        var context = Context();
        var calls = new List<string>();
        var dispatcher = new ActionDispatcher([new Handler(calls)]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => dispatcher.DispatchAsync(context));
        context.ConfigureActions([], WorkflowExecutionMoment.BeforeStart);
        await dispatcher.DispatchAsync(context);
        Assert.Empty(calls);
    }

    [Fact]
    public async Task FailureStopsRemainingActionsAndClearsCurrentAction()
    {
        var context = Context();
        context.ConfigureActions([Action("first", 1), Action("second", 2)], WorkflowExecutionMoment.BeforeStart);
        var calls = new List<string>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ActionDispatcher([new Handler(calls, fail: true)]).DispatchAsync(context));
        Assert.Equal(new[] { "first" }, calls);
        Assert.False(context.ContainsItem("LastActionName"));
        Assert.Null(context.CurrentAction);
    }

    [Fact]
    public async Task CancellationPreventsExecution()
    {
        var context = Context();
        context.ConfigureActions([Action("first", 1)], WorkflowExecutionMoment.BeforeStart);
        var calls = new List<string>();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new ActionDispatcher([new Handler(calls)]).DispatchAsync(context, new CancellationToken(true)));
        Assert.Empty(calls);
    }

    [Fact]
    public async Task DirectExecutorRejectsUnconfiguredAction()
    {
        var context = Context();
        context.ConfigureActions([], WorkflowExecutionMoment.BeforeStart);
        var calls = new List<string>();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new WorkflowActionExecutor([new Handler(calls)]).ExecuteAsync(Action("unconfigured", 1), context));
        Assert.Empty(calls);
        Assert.False(context.ContainsItem("LastActionId"));
    }

    [Fact]
    public async Task FailedActionPreservesPreviousSuccessfulMetadata()
    {
        var context = Context();
        var first = Action("success", 1);
        var second = Action("failure", 2);
        context.ConfigureActions([first, second], WorkflowExecutionMoment.BeforeStart);
        var calls = new List<string>();
        await new WorkflowActionExecutor([new Handler(calls)]).ExecuteAsync(first, context);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new WorkflowActionExecutor([new Handler(calls, fail: true)]).ExecuteAsync(second, context));
        Assert.Equal("success", context.GetItem<string>("LastActionName"));
        Assert.Null(context.CurrentAction);
    }

    private sealed class Handler(List<string> calls, WorkflowActionType type = WorkflowActionType.CreateTask, bool fail = false) : IActionCapability
    {
        public WorkflowActionType ActionType => type;
        public Task ExecuteAsync(ProcessExecutionContext context, CancellationToken cancellationToken = default)
        {
            calls.Add(context.CurrentAction!.Name);
            if (fail) throw new InvalidOperationException("Test failure");
            return Task.CompletedTask;
        }
    }
}
