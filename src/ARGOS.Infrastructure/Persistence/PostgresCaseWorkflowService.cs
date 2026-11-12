using ARGOS.Application.ProcessEngine.Evaluation;
using ARGOS.Application.ProcessEngine.Resolution;
using ARGOS.Domain.Aggregates.CaseFile.Enums;
using ARGOS.Domain.Aggregates.Organization.ValueObjects;
using ARGOS.Domain.Aggregates.Procedure.Enums;
using ARGOS.Domain.Aggregates.Workflow.Enums;
using ARGOS.Domain.Aggregates.Workflow.ValueObjects;
using ARGOS.Domain.Aggregates.Workflow.Activities.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ARGOS.Infrastructure.Persistence;

/// <summary>Internal transactional routing. Callers must authorize the operation and supply trusted facts.</summary>
public sealed class PostgresCaseWorkflowService(DbContextOptions<ArgosDbContext> options)
{
    public sealed record Actor(Guid OrganizationId, string Person, string Session, string Request);

    public async Task<CaseWorkflowRun> StartAsync(Guid caseId, WorkflowId workflowId,
        Guid initialActivityId, Actor actor, CancellationToken ct = default)
    {
        await using var db = new ArgosDbContext(options);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var caseFile = await db.CaseFiles.FromSqlInterpolated($"SELECT * FROM \"CaseFiles\" WHERE \"Id\" = {caseId} FOR UPDATE").SingleAsync(ct);
        await CheckOwnership(db, caseId, actor, ct);
        if (caseFile.Status != CaseFileStatus.InProgress)
            throw new InvalidOperationException("El expediente debe estar en trámite para iniciar su flujo.");
        var workflow = await db.Workflows.SingleAsync(x => x.Id == workflowId && x.OrganizationId == OrganizationId.From(actor.OrganizationId), ct);
        if (workflow.Status != WorkflowStatus.Active || !await db.Procedures.AnyAsync(x => x.Id == workflow.ProcedureId && x.Status == ProcedureStatus.Published, ct))
            throw new InvalidOperationException("El flujo y la versión del trámite deben estar vigentes.");
        var starts = workflow.Activities.Where(x => !workflow.Transitions.Any(t => t.DestinationActivityId == x.Id)).ToArray();
        if (starts.Length != 1 || starts[0].Id.Value != initialActivityId)
            throw new InvalidOperationException("El flujo requiere una actividad inicial inequívoca.");
        if (await db.Set<CaseWorkflowRun>().AnyAsync(x => x.CaseFileId == caseId && x.WorkflowId == workflowId, ct))
            throw new InvalidOperationException("El expediente ya tiene una ejecución de este flujo.");
        var run = new CaseWorkflowRun { Id = Guid.NewGuid(), CaseFileId = caseId, OrganizationId = actor.OrganizationId,
            WorkflowId = workflowId, CurrentActivityId = initialActivityId, Revision = 0 };
        db.Add(run);
        Record(db, run, null, actor);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return run;
    }

    public async Task<CaseWorkflowRun> AdvanceAsync(Guid runId, long expectedRevision,
        IReadOnlyDictionary<string, bool> trustedFacts, Actor actor, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(trustedFacts);
        await using var db = new ArgosDbContext(options);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var run = await db.Set<CaseWorkflowRun>().SingleAsync(x => x.Id == runId && x.OrganizationId == actor.OrganizationId, ct);
        var caseFile = await db.CaseFiles.FromSqlInterpolated($"SELECT * FROM \"CaseFiles\" WHERE \"Id\" = {run.CaseFileId} FOR UPDATE").SingleAsync(ct);
        await db.Entry(run).ReloadAsync(ct);
        await CheckOwnership(db, run.CaseFileId, actor, ct);
        if (run.Revision != expectedRevision) throw new DbUpdateConcurrencyException("El flujo ya cambió; actualice la consulta.");
        if (caseFile.Status != CaseFileStatus.InProgress)
            throw new InvalidOperationException("Solo se puede avanzar un expediente en trámite.");
        var workflow = await db.Workflows.AsNoTracking().SingleAsync(x => x.Id == run.WorkflowId, ct);
        if (workflow.Status != WorkflowStatus.Active)
            throw new InvalidOperationException("El flujo no está activo.");
        var context = await new PostgresWorkflowLoader(db).LoadAsync(run.WorkflowId, OrganizationId.From(actor.OrganizationId),
            workflow.ProcedureId, WorkflowActivityId.Create(run.CurrentActivityId), run.Id, ct);
        foreach (var fact in trustedFacts) context.SetItem(fact.Key, fact.Value);
        var next = await new TransitionResolver(new ConditionEvaluator()).ResolveAsync(context, ct)
            ?? throw new InvalidOperationException("No existe una transición habilitada; el expediente conserva su actividad.");
        var previous = run.CurrentActivityId;
        run.CurrentActivityId = next.Id.Value;
        run.Revision = checked(run.Revision + 1);
        Record(db, run, previous, actor);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return run;
    }

    private static async Task CheckOwnership(ArgosDbContext db, Guid caseId, Actor actor, CancellationToken ct)
    {
        if (actor.OrganizationId == Guid.Empty || string.IsNullOrWhiteSpace(actor.Session))
            throw new InvalidOperationException("Se requiere una sesión institucional identificada.");
        // Legacy CaseFile has no organization column: require the immutable creation record.
        if (!await db.AuditEntries.AnyAsync(x => x.SubjectType == "CaseFile" && x.SubjectReference == caseId.ToString()
            && x.ActionCode == "CaseFile.Created" && x.OrganizationId == actor.OrganizationId && x.Outcome == "Succeeded", ct))
            throw new InvalidOperationException("No se pudo acreditar la institución del expediente.");
    }

    private static void Record(ArgosDbContext db, CaseWorkflowRun run, Guid? previous, Actor actor)
    {
        var audit = AuditEntry.Create(actor.OrganizationId, actor.Person, actor.Session,
            previous is null ? "CaseWorkflow.Started" : "CaseWorkflow.Advanced", "CaseFile", run.CaseFileId.ToString(),
            "Succeeded", actor.Request, $"run:{run.Id};revision:{run.Revision};from:{previous};to:{run.CurrentActivityId}");
        db.Add(audit);
        db.Add(new CaseWorkflowStep { Id = Guid.NewGuid(), RunId = run.Id, Revision = run.Revision,
            FromActivityId = previous, ToActivityId = run.CurrentActivityId, AuditEntryId = audit.Id });
    }
}
