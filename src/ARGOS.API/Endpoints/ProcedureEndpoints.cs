using ARGOS.Application.Modules.Procedures;
using ARGOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ARGOS.API.Endpoints;

public static class ProcedureEndpoints
{
    public static IEndpointRouteBuilder MapProcedureEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/procedures")
            .WithTags("Procedures")
            .RequireAuthorization("ProcedureAdministration");
        group.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            var http = context.HttpContext;
            if (!Guid.TryParse(http.User.FindFirstValue("organization_id"), out var organization) || organization == Guid.Empty) return Results.Forbid();
            var db = http.RequestServices.GetRequiredService<ArgosDbContext>();
            if (Guid.TryParse(http.Request.RouteValues["id"]?.ToString(), out var scopedId)
                && !await db.AuditEntries.AnyAsync(x => x.OrganizationId == organization && x.ActionCode == "Procedure.Created"
                    && x.SubjectType == "Procedure" && x.SubjectReference == scopedId.ToString() && x.Outcome == "Succeeded", http.RequestAborted))
                return Results.NotFound();
            var actor = context.HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            foreach (var argument in context.Arguments)
            {
                var decision = argument switch
                {
                    ProcedureDecisionRequest direct => direct,
                    SubmitProcedureForReviewRequest submit => submit.Decision,
                    EmergencyPublishProcedureRequest emergency => emergency.Decision,
                    _ => null
                };
                if (decision?.ActorReference is not null && !string.Equals(decision.ActorReference, actor, StringComparison.Ordinal))
                    return Results.Forbid();
            }
            var creating = context.Arguments.Any(x => x is CreateProcedureRequest or CreateProcedureVersionRequest);
            if (!creating) return await http.RequestServices.GetRequiredService<IProcedureDecisionGuard>().InvokeAsync(context, next);
            await using var transaction = await db.Database.BeginTransactionAsync(http.RequestAborted);
            var result = await next(context);
            if (result is IStatusCodeHttpResult { StatusCode: >= 200 and < 300 } && result is IValueHttpResult { Value: ProcedureDto created })
            {
                db.AuditEntries.Add(AuditEntry.Create(organization, actor!, http.User.FindFirstValue("session_id"), "Procedure.Created",
                    "Procedure", created.Id.ToString(), "Succeeded", http.TraceIdentifier));
                await db.SaveChangesAsync(http.RequestAborted); await transaction.CommitAsync(http.RequestAborted);
            }
            return result;
        });

        group.MapPost("/{id:guid}/{action}/verification", (Guid id, string action, ProcedureVerificationRequest request,
            HttpContext http, ProcedureDecisionGuard guard) => guard.Begin(http, id, action, request)).RequireRateLimiting("login");

        group.MapGet("/authority-options", async (HttpContext http, PersonnelDbContext personnel, ArgosDbContext db,
            ARGOS.Application.Common.Persistence.IOrganizationSetupRepository organizations, TimeProvider clock, CancellationToken ct) =>
        {
            var organization = Guid.Parse(http.User.FindFirstValue("organization_id")!);
            if (!Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var personId)) return Results.Forbid();
            var org = ARGOS.Domain.Aggregates.Organization.ValueObjects.OrganizationId.From(organization);
            var person = await personnel.People.AsNoTracking().SingleOrDefaultAsync(x => x.Id == personId && x.OrganizationId == org, ct);
            var setup = await organizations.GetPublishedAsync(org, ct);
            if (person is null || setup is null) return Results.Forbid();
            return Results.Ok(new { PersonId = personId,
                Permissions = http.User.FindAll("permission").Select(x => x.Value).Distinct().ToArray(),
                Assignments = person.Assignments.Where(x => x.IsEffectiveAt(clock.GetUtcNow())).Select(x => new { x.Id, x.PositionId }),
                setup.Positions, setup.Competences,
                Policies = await db.OperationPolicies.AsNoTracking().Where(x => x.OrganizationId == organization).ToListAsync(ct) });
        });

        // Administrative catalogue includes drafts; the operational catalogue below remains published-only.
        group.MapGet("/administration", async (HttpContext http, ArgosDbContext db, CancellationToken ct) =>
        {
            var org = Guid.Parse(http.User.FindFirstValue("organization_id")!);
            var references = await db.AuditEntries.AsNoTracking().Where(x => x.OrganizationId == org
                && x.ActionCode == "Procedure.Created" && x.SubjectType == "Procedure" && x.Outcome == "Succeeded")
                .Select(x => x.SubjectReference).Distinct().ToListAsync(ct);
            var ids = references.Where(x => Guid.TryParse(x, out _))
                .Select(x => ARGOS.Domain.Aggregates.Procedure.ValueObjects.ProcedureId.From(Guid.Parse(x))).ToArray();
            var procedures = await db.Procedures.AsNoTracking().Where(x => ids.Contains(x.Id))
                .OrderBy(x => x.Code).ThenByDescending(x => x.Version).ToListAsync(ct);
            return Results.Ok(procedures.Select(ProcedureDto.From));
        });

        group.MapGet("/", async (
            HttpContext http, ArgosDbContext db,
            ProcedureService service,
            CancellationToken cancellationToken) =>
        {
            var procedures = await service.ListAsync(cancellationToken);
            var organization = Guid.Parse(http.User.FindFirstValue("organization_id")!);
            var owned = await db.AuditEntries.Where(x => x.OrganizationId == organization && x.ActionCode == "Procedure.Created"
                && x.SubjectType == "Procedure" && x.Outcome == "Succeeded").Select(x => x.SubjectReference).ToListAsync(cancellationToken);
            return Results.Ok(procedures.Where(x => owned.Contains(x.Id.ToString())).ToArray());
        })
        .WithName("ListProcedures");

        group.MapGet("/{id:guid}", async (
            Guid id,
            ProcedureService service,
            CancellationToken cancellationToken) =>
        {
            var procedure = await service.GetByIdAsync(id, cancellationToken);

            return procedure is null
                ? Results.NotFound(new
                {
                    Code = "Procedure.NotFound",
                    Description = "El procedimiento solicitado no existe."
                })
                : Results.Ok(procedure);
        })
        .WithName("GetProcedureById");

        group.MapGet("/{id:guid}/versions", async (
            Guid id,
            ProcedureService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ListVersionsAsync(id, cancellationToken);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.NotFound(result.Error);
        })
        .WithName("ListProcedureVersions");

        group.MapPost("/", async (
            CreateProcedureRequest request,
            ProcedureService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(
                request.Code,
                request.Name,
                request.Description,
                cancellationToken);

            if (result.IsSuccess)
            {
                return Results.Created(
                    $"/api/procedures/{result.Value.Id}",
                    result.Value);
            }

            return result.Error == ProcedureErrors.InvalidData
                ? Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["request"] = [result.Error.Description]
                })
                : Results.Conflict(result.Error);
        })
        .WithName("CreateProcedure");

        group.MapPost("/{id:guid}/versions", async (
            Guid id,
            CreateProcedureVersionRequest request,
            ProcedureService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateVersionAsync(
                id,
                request.Name,
                request.Description,
                cancellationToken);

            return result.IsSuccess
                ? Results.Created(
                    $"/api/procedures/{result.Value.Id}",
                    result.Value)
                : ToHttpResult(result);
        })
        .WithName("CreateProcedureVersion");

        group.MapPost("/{id:guid}/submit-review", async (
            Guid id,
            SubmitProcedureForReviewRequest request,
            ProcedureService service,
            CancellationToken cancellationToken) =>
        {
            if (request.Policy is null || request.Decision is null)
                return MissingDecisionData();

            return ToHttpResult(await service.SubmitForReviewAsync(
                id,
                request.Policy.ToInput(),
                request.Decision.ToInput(),
                cancellationToken));
        })
        .WithName("SubmitProcedureForReview");

        group.MapPost("/{id:guid}/approve", async (
            Guid id,
            ProcedureDecisionRequest request,
            ProcedureService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.ApproveAsync(
                id,
                request.ToInput(),
                cancellationToken)))
        .WithName("ApproveProcedure");

        group.MapPost("/{id:guid}/reject", async (
            Guid id,
            ProcedureDecisionRequest request,
            ProcedureService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.RejectAsync(
                id,
                request.ToInput(),
                cancellationToken)))
        .WithName("RejectProcedure");

        group.MapPost("/{id:guid}/publish", async (
            Guid id,
            ProcedureDecisionRequest request,
            ProcedureService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.PublishAsync(
                id,
                request.ToInput(),
                cancellationToken)))
        .WithName("PublishProcedure");

        group.MapPost("/{id:guid}/publish-emergency", async (
            Guid id,
            EmergencyPublishProcedureRequest request,
            ProcedureService service,
            CancellationToken cancellationToken) =>
        {
            if (request.Decision is null)
                return MissingDecisionData();

            return ToHttpResult(await service.PublishEmergencyAsync(
                id,
                request.RatificationDueAtUtc,
                request.Decision.ToInput(),
                cancellationToken));
        })
        .WithName("EmergencyPublishProcedure");

        group.MapPost("/{id:guid}/ratify-emergency", async (
            Guid id,
            ProcedureDecisionRequest request,
            ProcedureService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.RatifyEmergencyAsync(
                id,
                request.ToInput(),
                cancellationToken)))
        .WithName("RatifyEmergencyProcedure");

        group.MapPost("/{id:guid}/retire", async (
            Guid id,
            ProcedureDecisionRequest request,
            ProcedureService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.RetireAsync(
                id,
                request.ToInput(),
                cancellationToken)))
        .WithName("RetireProcedure");

        group.MapPost("/{id:guid}/archive", async (
            Guid id,
            ProcedureDecisionRequest request,
            ProcedureService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.ArchiveAsync(
                id,
                request.ToInput(),
                cancellationToken)))
        .WithName("ArchiveProcedure");

        return endpoints;
    }

    private static IResult ToHttpResult(
        ARGOS.SharedKernel.Results.Result<ProcedureDto> result)
    {
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        if (result.Error == ProcedureErrors.NotFound)
        {
            return Results.NotFound(result.Error);
        }

        if (result.Error == ProcedureErrors.InvalidData
            || result.Error == ProcedureErrors.InvalidDecisionData
            || result.Error.Code is "Procedure.InvalidApprovalPolicy"
                or "Procedure.InvalidRatificationDeadline")
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["request"] = [result.Error.Description]
            });
        }

        return Results.Conflict(result.Error);
    }

    private static IResult MissingDecisionData()
        => Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["request"] = ["La política y los datos de decisión son obligatorios."]
        });
}

public sealed record CreateProcedureRequest(
    string? Code,
    string? Name,
    string? Description);

public sealed record CreateProcedureVersionRequest(
    string? Name,
    string? Description);

public sealed record SubmitProcedureForReviewRequest(
    ProcedureApprovalPolicyRequest? Policy,
    ProcedureDecisionRequest? Decision);

public sealed record EmergencyPublishProcedureRequest(
    DateTime RatificationDueAtUtc,
    ProcedureDecisionRequest? Decision);

public sealed record ProcedureApprovalPolicyRequest(
    int RequiredApprovalCount = 1,
    bool SeparationOfDutiesRequired = true,
    bool EmergencyPublicationAllowed = false,
    bool EmergencyRatificationRequired = false)
{
    public ProcedureApprovalPolicyInput ToInput()
        => new(
            RequiredApprovalCount,
            SeparationOfDutiesRequired,
            EmergencyPublicationAllowed,
            EmergencyRatificationRequired);
}

public sealed record ProcedureDecisionRequest(
    string? ActorReference,
    string? AssignmentReference,
    string? PositionReference,
    string? CompetenceCode,
    string? PolicyVersion,
    string? AuthorizationReference,
    string? Comments,
    Guid PolicyId = default,
    Guid ChallengeId = default,
    string? Code = null)
{
    public ProcedureDecisionInput ToInput()
        => new(
            ActorReference,
            AssignmentReference,
            PositionReference,
            CompetenceCode,
            PolicyVersion,
            AuthorizationReference,
            Comments);
}
