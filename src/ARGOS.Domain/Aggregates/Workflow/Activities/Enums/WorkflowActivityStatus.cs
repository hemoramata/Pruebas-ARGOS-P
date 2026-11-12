namespace ARGOS.Domain.Aggregates.Workflow.Activities.Enums;

/// <summary>
/// Represents the lifecycle status of a workflow activity.
/// </summary>
public enum WorkflowActivityStatus
{
    Draft = 1,

    Active = 2,

    Inactive = 3,

    Archived = 4
}