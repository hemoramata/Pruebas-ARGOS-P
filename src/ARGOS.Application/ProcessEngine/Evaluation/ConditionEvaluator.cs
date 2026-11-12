using ARGOS.Application.ProcessEngine.Contracts;
using ARGOS.Application.ProcessEngine.Execution;
using ARGOS.Domain.Aggregates.Workflow.Conditions;

namespace ARGOS.Application.ProcessEngine.Evaluation;

/// <summary>
/// Default implementation responsible for evaluating workflow conditions.
/// </summary>
public sealed class ConditionEvaluator : IConditionEvaluator
{
    public Task<bool> EvaluateAsync(
        WorkflowCondition condition,
        ProcessExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(context);

        cancellationToken.ThrowIfCancellationRequested();

        // Interpret the documented boolean comparison syntax only. Never execute
        // expressions as code or treat unavailable facts as satisfied conditions.
        var expression = condition.Expression;
        var match = System.Text.RegularExpressions.Regex.Match(
            expression,
            @"\A\s*([A-Za-z_][A-Za-z0-9_]*)\s*(==|!=)\s*(true|false)\s*\z",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));

        if (!match.Success)
            throw new InvalidOperationException("La condición debe comparar un dato lógico con true o false mediante == o !=.");

        var key = match.Groups[1].Value;
        if (!context.Items.TryGetValue(key, out var value) || value is not bool actual)
            throw new InvalidOperationException($"La condición requiere el dato lógico '{key}'.");

        var expected = bool.Parse(match.Groups[3].Value);
        return Task.FromResult(match.Groups[2].Value == "==" ? actual == expected : actual != expected);
    }
}
