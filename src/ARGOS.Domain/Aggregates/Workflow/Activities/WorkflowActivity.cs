using ARGOS.Domain.Aggregates.Workflow.Activities.Enums;
using ARGOS.Domain.Aggregates.Workflow.Activities.ValueObjects;
using ARGOS.SharedKernel.Abstractions;

namespace ARGOS.Domain.Aggregates.Workflow.Activities;

/// <summary>
/// Represents a single activity within a workflow.
/// Activities define the work that must be performed during
/// the execution of a workflow.
/// </summary>
public sealed class WorkflowActivity : Entity<WorkflowActivityId>
{
    private WorkflowActivity()
    {
    }

    private WorkflowActivity(
        WorkflowActivityId id,
        string code,
        string name,
        string description,
        int displayOrder)
        : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        if (displayOrder < 1)
            throw new ArgumentOutOfRangeException(nameof(displayOrder));

        Code = code.Trim();
        Name = name.Trim();
        Description = description.Trim();
        DisplayOrder = displayOrder;

        Status = WorkflowActivityStatus.Draft;
    }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public int DisplayOrder { get; private set; }

    public WorkflowActivityStatus Status { get; private set; }

    public static WorkflowActivity Create(
        string code,
        string name,
        string description,
        int displayOrder)
    {
        return new WorkflowActivity(
            WorkflowActivityId.New(),
            code,
            name,
            description,
            displayOrder);
    }

    public void Activate()
        => Status = WorkflowActivityStatus.Active;

    public void Deactivate()
        => Status = WorkflowActivityStatus.Inactive;

    public void Archive()
        => Status = WorkflowActivityStatus.Archived;
}