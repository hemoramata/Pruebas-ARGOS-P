using ARGOS.Domain.Aggregates.Workflow.Actions.Enums;
using ARGOS.Domain.Aggregates.Workflow.Actions.ValueObjects;
using ARGOS.Domain.Aggregates.Workflow.Activities.ValueObjects;
using ARGOS.SharedKernel.Abstractions;

namespace ARGOS.Domain.Aggregates.Workflow.Actions;

/// <summary>
/// Represents an automated action executed by ARGOS during
/// the lifecycle of a workflow activity.
/// </summary>
public sealed class WorkflowAction : Entity<WorkflowActionId>
{
    private WorkflowAction()
    {
    }

    private WorkflowAction(
        WorkflowActionId id,
        WorkflowActivityId activityId,
        string name,
        WorkflowActionType actionType,
        WorkflowExecutionMoment executionMoment,
        int executionOrder,
        WorkflowActionConfiguration configuration)
        : base(id)
    {
        ArgumentNullException.ThrowIfNull(activityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(configuration);

        if (executionOrder < 1)
            throw new ArgumentOutOfRangeException(nameof(executionOrder));

        ActivityId = activityId;
        Name = name.Trim();
        ActionType = actionType;
        ExecutionMoment = executionMoment;
        ExecutionOrder = executionOrder;
        Configuration = configuration;

        IsActive = true;
    }

    /// <summary>
    /// Workflow activity associated with this action.
    /// </summary>
    public WorkflowActivityId ActivityId { get; private set; } = default!;

    /// <summary>
    /// Friendly name of the action.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Type of automated action.
    /// </summary>
    public WorkflowActionType ActionType { get; private set; }

    /// <summary>
    /// Indicates when the action should be executed.
    /// </summary>
    public WorkflowExecutionMoment ExecutionMoment { get; private set; }

    /// <summary>
    /// Execution order within the same execution moment.
    /// </summary>
    public int ExecutionOrder { get; private set; }

    /// <summary>
    /// Configuration required by the Action Executor.
    /// </summary>
    public WorkflowActionConfiguration Configuration { get; private set; } = default!;

    /// <summary>
    /// Indicates whether the action is enabled.
    /// </summary>
    public bool IsActive { get; private set; }

    public static WorkflowAction Create(
        WorkflowActivityId activityId,
        string name,
        WorkflowActionType actionType,
        WorkflowExecutionMoment executionMoment,
        int executionOrder,
        WorkflowActionConfiguration configuration)
    {
        return new WorkflowAction(
            WorkflowActionId.New(),
            activityId,
            name,
            actionType,
            executionMoment,
            executionOrder,
            configuration);
    }

    /// <summary>
    /// Enables the action.
    /// </summary>
    public void Activate()
    {
        IsActive = true;
    }

    /// <summary>
    /// Disables the action.
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
    }
}