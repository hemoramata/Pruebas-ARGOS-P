using ARGOS.Application.ProcessEngine.Execution;
using ARGOS.Domain.Aggregates.Workflow.ValueObjects;
using ARGOS.Domain.Aggregates.Workflow.Activities.ValueObjects;
using ARGOS.Domain.Aggregates.Organization.ValueObjects;
using ARGOS.Domain.Aggregates.Procedure.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ARGOS.Infrastructure.Persistence;

public sealed class PostgresWorkflowLoader(ArgosDbContext db)
{
    public async Task<ProcessExecutionContext> LoadAsync(WorkflowId workflowId,
        OrganizationId organizationId, ProcedureId procedureVersionId,
        WorkflowActivityId currentActivityId, Guid processInstanceId, CancellationToken ct = default)
    {
        var workflow = await db.Workflows.AsNoTracking().AsSplitQuery()
            .SingleOrDefaultAsync(x => x.Id == workflowId && x.OrganizationId == organizationId &&
                x.ProcedureId == procedureVersionId, ct)
            ?? throw new InvalidOperationException("El flujo no existe para la institución y versión solicitadas.");
        var activity = workflow.Activities.SingleOrDefault(x => x.Id == currentActivityId)
            ?? throw new InvalidOperationException("La actividad no pertenece al flujo solicitado.");
        var context = new ProcessExecutionContext(workflow, activity, processInstanceId);
        context.ConfigureRouting(workflow.Activities, workflow.Transitions, workflow.Conditions);
        return context;
    }
}
