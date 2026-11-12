using System.Security.Claims;
using ARGOS.Application.Common.Persistence;
using ARGOS.Application.Security;
using ARGOS.Domain.Aggregates.Organization.ValueObjects;
using ARGOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ARGOS.API.Endpoints;

public static class InstitutionalPolicyEndpoints
{
    public sealed record PublishPolicyRequest(Guid AssignmentId, string CompetenceCode, Guid ProofContentId,
        int ExpectedVersion, InstitutionalPolicyRules Rules, Guid ChallengeId = default, string? Code = null);

    public static IEndpointRouteBuilder MapInstitutionalPolicyEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/institutional-policies").RequireAuthorization("PersonnelAdministration");
        group.AddEndpointFilter(async (context, next) => {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
        group.MapGet("/", async (HttpContext http, ArgosDbContext db, CancellationToken ct) => {
            if (!Guid.TryParse(http.User.FindFirstValue("organization_id"), out var org)) return Results.Forbid();
            return Results.Ok(await db.OperationPolicies.AsNoTracking().Where(x => x.OrganizationId == org).ToListAsync(ct));
        });
        group.MapPost("/verification", (PublishPolicyRequest request, HttpContext http, ArgosDbContext db,
            InstitutionalAuthorityResolver resolver, IOrganizationSetupRepository organizations, OperationVerification verification,
            TimeProvider clock) => Publish(request, http, db, resolver, organizations, verification, clock, false)).RequireRateLimiting("login");
        group.MapPost("/", (PublishPolicyRequest request, HttpContext http, ArgosDbContext db,
            InstitutionalAuthorityResolver resolver, IOrganizationSetupRepository organizations, OperationVerification verification,
            TimeProvider clock) => Publish(request, http, db, resolver, organizations, verification, clock, true));
        return routes;
    }

    private static async Task<IResult> Publish(PublishPolicyRequest request, HttpContext http, ArgosDbContext db,
        InstitutionalAuthorityResolver resolver, IOrganizationSetupRepository organizations, OperationVerification verification,
        TimeProvider clock, bool execute)
    {
        if (!InstitutionalOperationPolicy.Valid(request.Rules) || request.ExpectedVersion < 0) return Results.BadRequest(new { code = "Policy.InvalidRules" });
        if (!Guid.TryParse(http.User.FindFirstValue("organization_id"), out var org)
            || !Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var person)
            || !Guid.TryParse(http.User.FindFirstValue("session_id"), out var session)) return Results.Forbid();
        var ct = http.RequestAborted;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var key = $"policy:{org:D}:{request.Rules.ProcedureCode}";
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({key}))", ct);
        var authority = await resolver.ResolveAsync(person, org, request.AssignmentId, request.CompetenceCode, "personnel.manage", ct);
        if (authority is null) return Results.Forbid();
        var setup = await organizations.GetPublishedAsync(OrganizationId.From(org), ct);
        if (setup is null || request.Rules.Authorities.Any(rule => !setup.Positions.Any(position => position.Id == rule.PositionId
            && setup.Competences.Any(competence => competence.Code == rule.CompetenceCode && position.CompetenceIds.Contains(competence.Id)))))
            return Results.BadRequest(new { code = "Policy.UnknownPositionOrCompetence" });
        var proof = await db.ProtectedCaseContents.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.ProofContentId, ct);
        if (proof is null || !await db.AuditEntries.AnyAsync(x => x.OrganizationId == org && x.ActionCode == "CaseFile.Created"
            && x.SubjectType == "CaseFile" && x.SubjectReference == proof.CaseFileId.ToString() && x.Outcome == "Succeeded", ct))
            return Results.BadRequest(new { code = "Policy.RetainedInstitutionalProofRequired" });
        var version = await db.OperationPolicies.Where(x => x.OrganizationId == org && x.ProcedureCode == request.Rules.ProcedureCode)
            .Select(x => (int?)x.Version).MaxAsync(ct) ?? 0;
        if (version != request.ExpectedVersion) return Results.Conflict(new { code = "Policy.ConcurrentVersion" });
        var fingerprint = ProcedureDecisionGuard.Fingerprint(new { Operation = "policy.publish", authority,
            request.ProofContentId, request.ExpectedVersion, request.Rules });
        if (!execute)
        {
            try {
                var challenge = await verification.BeginAsync(person, org, session, "personnel.manage", fingerprint, ct);
                if (challenge is null) return Results.StatusCode(429);
                db.AuditEntries.Add(Audit(http, org, person, "Policy.VerificationIssued", challenge.Id));
                await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
                return Results.Ok(challenge);
            }
            catch (Exception error) when (error is InvalidOperationException or TimeoutException
                || error is OperationCanceledException && !ct.IsCancellationRequested) { return Results.StatusCode(503); }
        }
        if (!await verification.ConsumeAsync(person, org, session, "personnel.manage", fingerprint, request.ChallengeId, request.Code, ct))
        {
            db.AuditEntries.Add(AuditEntry.Create(org, person.ToString(), session.ToString(), "Policy.VerificationRejected",
                "InstitutionalPolicy", request.Rules.ProcedureCode, "Rejected", http.TraceIdentifier));
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
            return Results.Json(new { code = "Policy.AdditionalVerificationRequired" }, statusCode: 403);
        }
        var policy = InstitutionalOperationPolicy.Create(org, version + 1, proof.Id, person, authority.Assignment,
            request.ChallengeId, request.Rules, clock.GetUtcNow());
        db.OperationPolicies.Add(policy);
        db.AuditEntries.Add(Audit(http, org, person, "Policy.Published", policy.Id));
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return Results.Created($"/api/institutional-policies/{policy.Id}", policy);
    }

    private static AuditEntry Audit(HttpContext http, Guid org, Guid person, string action, Guid id) =>
        AuditEntry.Create(org, person.ToString(), http.User.FindFirstValue("session_id"), action, "InstitutionalPolicy",
            id.ToString(), "Succeeded", http.TraceIdentifier);
}
