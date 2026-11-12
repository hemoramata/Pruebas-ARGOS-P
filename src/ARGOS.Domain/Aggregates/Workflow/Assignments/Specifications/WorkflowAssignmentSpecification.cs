using ARGOS.Domain.Aggregates.Workflow.Assignments;

namespace ARGOS.Domain.Aggregates.Workflow.Assignments.Specifications;

/// <summary>
/// Defines the business rules for workflow assignments.
/// </summary>
public sealed class WorkflowAssignmentSpecification
{
    public void Validate(WorkflowAssignment assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        if (assignment.OrganizationUnitId is null)
            throw new InvalidOperationException(
                "Every workflow assignment must have an organizational unit.");
    }
}