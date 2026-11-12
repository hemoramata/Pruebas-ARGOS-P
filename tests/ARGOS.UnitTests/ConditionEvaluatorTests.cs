using ARGOS.Application.ProcessEngine.Evaluation;
using ARGOS.Application.ProcessEngine.Execution;
using ARGOS.Domain.Aggregates.Organization.ValueObjects;
using ARGOS.Domain.Aggregates.Procedure.ValueObjects;
using ARGOS.Domain.Aggregates.Workflow;
using ARGOS.Domain.Aggregates.Workflow.Activities;
using ARGOS.Domain.Aggregates.Workflow.Conditions;
using ARGOS.Domain.Aggregates.Workflow.Transitions.ValueObjects;

namespace ARGOS.UnitTests;

public sealed class ConditionEvaluatorTests
{
    [Theory]
    [InlineData("DocumentComplete == true", true, true)]
    [InlineData("DocumentComplete == true", false, false)]
    [InlineData("DocumentComplete == false", false, true)]
    [InlineData("DocumentComplete != false", true, true)]
    [InlineData("DocumentComplete != true", true, false)]
    [InlineData("  DocumentComplete  ==  true  ", true, true)]
    public async Task EvaluatesBooleanFact(string expression, bool fact, bool expected)
    {
        var context = Context();
        context.SetItem("DocumentComplete", fact);
        Assert.Equal(expected, await new ConditionEvaluator().EvaluateAsync(Condition(expression), context));
    }

    [Theory]
    [InlineData("true")]
    [InlineData("DocumentComplete = true")]
    [InlineData("DocumentComplete == true || true")]
    [InlineData("System.IO.File.Delete(\"x\")")]
    public async Task UnsupportedExpressionCannotAuthorize(string expression)
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ConditionEvaluator().EvaluateAsync(Condition(expression), Context()));
    }

    [Fact]
    public async Task MissingOrIncorrectlyTypedFactCannotAuthorizeEvenNegativeComparison()
    {
        var context = Context();
        var condition = Condition("DocumentComplete != true");
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ConditionEvaluator().EvaluateAsync(condition, context));
        context.SetItem("DocumentComplete", "false");
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ConditionEvaluator().EvaluateAsync(condition, context));
    }

    [Fact]
    public async Task CancelledEvaluationDoesNotRun()
    {
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new ConditionEvaluator().EvaluateAsync(Condition("DocumentComplete == true"), Context(), new CancellationToken(true)));
    }

    private static WorkflowCondition Condition(string expression) =>
        WorkflowCondition.Create(WorkflowTransitionId.New(), expression, 1);

    private static ProcessExecutionContext Context() => new(
        Workflow.Create(ProcedureId.New(), OrganizationId.New(), "TEST", "Prueba", "Flujo de prueba"),
        WorkflowActivity.Create("REV", "Revisión", "Revisión documental", 1), Guid.NewGuid());
}
