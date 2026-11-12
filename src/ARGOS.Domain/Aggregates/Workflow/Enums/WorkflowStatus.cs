namespace ARGOS.Domain.Aggregates.Workflow.Enums;

/// <summary>
/// Represents the lifecycle status of a workflow definition.
/// </summary>
public enum WorkflowStatus
{
    /// <summary>
    /// The workflow is being designed and cannot be used.
    /// </summary>
    Draft = 1,

    /// <summary>
    /// The workflow is available for creating new case files.
    /// </summary>
    Active = 2,

    /// <summary>
    /// The workflow is temporarily disabled.
    /// Existing case files continue normally.
    /// New case files cannot use this workflow.
    /// </summary>
    Inactive = 3,

    /// <summary>
    /// The workflow has been retired and can no longer be used.
    /// It is kept only for historical and auditing purposes.
    /// </summary>
    Archived = 4
}