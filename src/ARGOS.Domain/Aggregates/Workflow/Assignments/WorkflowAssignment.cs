using ARGOS.Domain.Aggregates.Organization.ValueObjects;
using ARGOS.Domain.Aggregates.OrganizationUnit.ValueObjects;
using ARGOS.Domain.Aggregates.Workflow.Activities.ValueObjects;
using ARGOS.Domain.Aggregates.Workflow.Assignments.ValueObjects;
using ARGOS.SharedKernel.Abstractions;

namespace ARGOS.Domain.Aggregates.Workflow.Assignments;

/// <summary>
/// Represents the assignment rule for a workflow activity.
/// Determines which organizational unit is responsible for executing
/// the activity.
/// </summary>
public sealed class WorkflowAssignment : Entity<WorkflowAssignmentId>
{
    private WorkflowAssignment()
    {
    }

    private WorkflowAssignment(
        WorkflowAssignmentId id,
        WorkflowActivityId activityId,
        OrganizationUnitId organizationUnitId)
        : base(id)
    {
        ArgumentNullException.ThrowIfNull(activityId);
        ArgumentNullException.ThrowIfNull(organizationUnitId);

        ActivityId = activityId;
        OrganizationUnitId = organizationUnitId;
    }

    /// <summary>
    /// Workflow activity associated with this assignment.
    /// </summary>
    public WorkflowActivityId ActivityId { get; private set; } = default!;

    /// <summary>
    /// Organizational unit responsible for executing the activity.
    /// </summary>
    public OrganizationUnitId OrganizationUnitId { get; private set; } = default!;

    public static WorkflowAssignment Create(
        WorkflowActivityId activityId,
        OrganizationUnitId organizationUnitId)
    {
        return new WorkflowAssignment(
            WorkflowAssignmentId.New(),
            activityId,
            organizationUnitId);
    }
}