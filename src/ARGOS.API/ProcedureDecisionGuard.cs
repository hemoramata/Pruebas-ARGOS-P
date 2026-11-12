using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ARGOS.API.Endpoints;
using ARGOS.Application.Security;
using ARGOS.Domain.Aggregates.Procedure.ValueObjects;
using ARGOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ARGOS.API;

public interface IProcedureDecisionGuard : IEndpointFilter { }
public sealed record ProcedureVerificationRequest(ProcedureDecisionRequest Decision, DateTime? RatificationDueAtUtc = null);

public sealed class ProcedureDecisionGuard(ArgosDbContext db, InstitutionalAuthorityResolver resolver,
    OperationVerification verification) : IProcedureDecisionGuard
{
    private sealed record Authorized(ResolvedAuthority Authority, InstitutionalOperationPolicy Policy, string Fingerprint);
    private async Task<Authorized?> Resolve(HttpContext http, Guid id, string action,
        ProcedureDecisionRequest decision, DateTime? deadline)
    {
        if (!Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var person)
            || !Guid.TryParse(http.User.FindFirstValue("organization_id"), out var organization)
            || !Guid.TryParse(decision.AssignmentReference, out var assignment)
            || decision.Comments?.Length > 2000 || !InstitutionalOperationPolicy.Actions.Contains(action)
            || action == "evidence-link") return null;
        var ct = http.RequestAborted;
        var authority = await resolver.ResolveAsync(person, organization, assignment, decision.CompetenceCode ?? "", "procedures.manage", ct);
        if (authority is null) return null;
        var policy = await db.OperationPolicies.AsNoTracking().SingleOrDefaultAsync(x => x.Id == decision.PolicyId && x.OrganizationId == organization, ct);
        var procedure = await db.Procedures.AsNoTracking().SingleOrDefaultAsync(x => x.Id == ProcedureId.From(id), ct);
        if (procedure is not null && db.Database.CurrentTransaction is not null)
        {
            var key = $"policy:{organization:D}:{procedure.Code}";
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({key}))", ct);
        }
        if (policy is null || procedure is null || policy.ProcedureCode != procedure.Code
            || !policy.Rules.Authorities.Any(x => x.Action == action && x.PositionId == authority.Position && x.CompetenceCode == authority.Competence)) return null;
        // A version under review remains bound to its policy. Draft submission uses the latest institutional policy.
        if (procedure.ApprovalPolicyVersion is not null && procedure.ApprovalPolicyVersion != policy.Id.ToString()) return null;
        if (procedure.ApprovalPolicyVersion is null && await db.OperationPolicies.AnyAsync(x => x.OrganizationId == organization
            && x.ProcedureCode == policy.ProcedureCode && x.Version > policy.Version, ct)) return null;
        var fingerprint = Fingerprint(new { person, organization, id, action, deadline, decision.Comments, authority,
            PolicyId = policy.Id, policy.RulesJson, procedure.Version, procedure.Status, Decisions = procedure.Decisions.Count });
        return new(authority, policy, fingerprint);
    }

    public async Task<IResult> Begin(HttpContext http, Guid id, string action, ProcedureVerificationRequest request)
    {
        if (request.Decision is null || id == Guid.Empty) return Results.BadRequest();
        var resolved = await Resolve(http, id, action, request.Decision, request.RatificationDueAtUtc);
        if (resolved is null) { await RecordDenied(http, id, "Procedure.AuthorityRejected"); return Results.Forbid(); }
        if (!Guid.TryParse(http.User.FindFirstValue("session_id"), out var session)) return Results.Forbid();
        try
        {
            var challenge = await verification.BeginAsync(resolved.Authority.Person, resolved.Authority.Organization, session,
                "procedures.manage", resolved.Fingerprint, http.RequestAborted);
            if (challenge is null) return Results.StatusCode(429);
            db.AuditEntries.Add(Audit(http, resolved.Authority, "Procedure.VerificationIssued", id, challenge.Id, "Succeeded"));
            await db.SaveChangesAsync(http.RequestAborted);
            return Results.Ok(challenge);
        }
        catch (Exception error) when (error is InvalidOperationException or TimeoutException
            || error is OperationCanceledException && !http.RequestAborted.IsCancellationRequested)
        { return Results.StatusCode(503); }
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var index = -1;
        ProcedureDecisionRequest? decision = null;
        DateTime? deadline = null;
        for (var i = 0; i < context.Arguments.Count; i++)
        {
            decision = context.Arguments[i] switch {
                ProcedureDecisionRequest direct => direct,
                SubmitProcedureForReviewRequest submit => submit.Decision,
                EmergencyPublishProcedureRequest emergency => emergency.Decision,
                _ => null };
            if (decision is not null) { index = i; deadline = (context.Arguments[i] as EmergencyPublishProcedureRequest)?.RatificationDueAtUtc; break; }
        }
        if (index < 0) return await next(context);
        var http = context.HttpContext;
        if (!Guid.TryParse(http.Request.RouteValues["id"]?.ToString(), out var id) || id == Guid.Empty
            || !Guid.TryParse(http.User.FindFirstValue("session_id"), out var session)) return Results.Forbid();
        var action = http.Request.Path.Value!.TrimEnd('/').Split('/')[^1];
        var ct = http.RequestAborted;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Serialize all decisions for the same procedure definition, including publication of sibling versions.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext(\"DefinitionId\"::text)) FROM \"Procedures\" WHERE \"Id\" = {id}", ct);
        var resolved = await Resolve(http, id, action, decision!, deadline);
        if (resolved is null)
        {
            await RecordDenied(http, id, "Procedure.AuthorityRejected");
            await transaction.CommitAsync(ct);
            return Results.Forbid();
        }
        var authority = resolved.Authority;
        if (!await verification.ConsumeAsync(authority.Person, authority.Organization, session, "procedures.manage",
            resolved.Fingerprint, decision!.ChallengeId, decision.Code, ct))
        {
            db.AuditEntries.Add(Audit(http, authority, "Procedure.VerificationRejected", id, decision.ChallengeId, "Rejected"));
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
            return Results.Json(new { code = "Procedure.AdditionalVerificationRequired" }, statusCode: 403);
        }
        var trusted = decision with { ActorReference = authority.Person.ToString(), AssignmentReference = authority.Assignment.ToString(),
            PositionReference = authority.Position.ToString(), CompetenceCode = authority.Competence, PolicyVersion = resolved.Policy.Id.ToString(),
            AuthorizationReference = $"policy:{resolved.Policy.Id:D};mfa:{decision.ChallengeId:D}" };
        var rules = resolved.Policy.Rules;
        context.Arguments[index] = context.Arguments[index] switch {
            SubmitProcedureForReviewRequest submit => submit with { Decision = trusted,
                Policy = new(rules.RequiredApprovalCount, rules.SeparationOfDutiesRequired, rules.EmergencyPublicationAllowed, rules.EmergencyPublicationAllowed) },
            EmergencyPublishProcedureRequest emergency => emergency with { Decision = trusted },
            _ => trusted };
        var result = await next(context);
        if (result is IStatusCodeHttpResult { StatusCode: >= 200 and < 300 })
        {
            db.AuditEntries.Add(Audit(http, authority, "Procedure.DecisionAuthorized", id, decision.ChallengeId, "Succeeded"));
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        }
        else
        {
            await transaction.RollbackAsync(ct);
            await transaction.DisposeAsync();
            db.ChangeTracker.Clear();
            // Do not accidentally save tracked aggregate mutations when recording a rejection.
            db.AuditEntries.Add(Audit(http, authority, "Procedure.DecisionRejected", id, decision.ChallengeId, "Rejected"));
            await db.SaveChangesAsync(ct);
        }
        return result;
    }

    private async Task RecordDenied(HttpContext http, Guid id, string action)
    {
        if (!Guid.TryParse(http.User.FindFirstValue("organization_id"), out var org)) return;
        db.AuditEntries.Add(AuditEntry.Create(org, http.User.FindFirstValue(ClaimTypes.NameIdentifier)!,
            http.User.FindFirstValue("session_id"), action, "Procedure", id.ToString(), "Rejected", http.TraceIdentifier));
        await db.SaveChangesAsync(http.RequestAborted);
    }

    internal static string Fingerprint(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
    private static AuditEntry Audit(HttpContext http, ResolvedAuthority authority, string action, Guid id, Guid challenge, string outcome) =>
        AuditEntry.Create(authority.Organization, authority.Person.ToString(), http.User.FindFirstValue("session_id"), action,
            "Procedure", id.ToString(), outcome, http.TraceIdentifier,
            $"assignment:{authority.Assignment};position:{authority.Position};competence:{authority.Competence};setup:{authority.Setup};verification:{challenge};appointment:{authority.AppointmentAuthorization}");
}
