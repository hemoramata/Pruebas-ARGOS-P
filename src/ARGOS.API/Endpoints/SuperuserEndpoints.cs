using System.Security.Claims;
using ARGOS.Application.Security;
using ARGOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ARGOS.API.Endpoints;

public static class SuperuserEndpoints
{
    public static IEndpointRouteBuilder MapSuperuserEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/superuser")
            .RequireAuthorization(policy => policy.RequireAuthenticatedUser().RequireClaim("amr", "mfa"));
        group.MapPost("/authorizations/begin", async (BeginRequest request, HttpContext http,
            AccessDbContext db, SuperuserAuthorizationService service, CancellationToken ct) =>
        {
            if (!Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var personId)
                || !Guid.TryParse(http.User.FindFirstValue("organization_id"), out var organizationId)
                || !Guid.TryParse(http.User.FindFirstValue("session_id"), out var sessionId))
                return Results.Forbid();
            var superuser = await db.Superusers.AsNoTracking().SingleOrDefaultAsync(x => x.PersonId == personId, ct);
            var authority = await db.Authorities.AsNoTracking().SingleOrDefaultAsync(x =>
                x.Id == request.AuthorityId && x.OrganizationId == organizationId, ct);
            if (superuser is null || authority is null) return Results.Forbid();
            try
            {
                var authorization = await service.BeginAsync(superuser, authority, sessionId,
                    request.Action, request.SubjectType, request.SubjectReference, ct);
                return authorization is null ? Results.Conflict(new { code = "Superuser.AuthorizationUnavailable" })
                    : Results.Ok(authorization);
            }
            catch (InvalidOperationException) { return Results.StatusCode(503); }
            catch (ArgumentException) { return Results.BadRequest(new { code = "Superuser.InvalidRequest" }); }
        });
        group.MapPost("/authorizations/complete", async (CompleteRequest request, HttpContext http,
            AccessDbContext db, SuperuserAuthorizationService service, CancellationToken ct) =>
        {
            if (!Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var personId)
                || !Guid.TryParse(http.User.FindFirstValue("organization_id"), out var organizationId)
                || !Guid.TryParse(http.User.FindFirstValue("session_id"), out var sessionId))
                return Results.Forbid();
            var superuser = await db.Superusers.AsNoTracking().SingleOrDefaultAsync(x => x.PersonId == personId, ct);
            var authority = await db.Authorities.AsNoTracking().SingleOrDefaultAsync(x =>
                x.Id == request.AuthorityId && x.OrganizationId == organizationId, ct);
            if (superuser is null || authority is null) return Results.Forbid();
            var authorization = new SuperuserAuthorization(request.AuthorizationId, personId,
                authority.Id, request.Action, request.SubjectType, request.SubjectReference, request.ExpiresAt);
            return service.Complete(superuser, authority, sessionId, authorization, request.Code)
                ? Results.NoContent() : Results.Unauthorized();
        });
        return endpoints;
    }

    public sealed record BeginRequest(Guid AuthorityId, string Action, string SubjectType, string SubjectReference);
    public sealed record CompleteRequest(Guid AuthorizationId, Guid AuthorityId, string Action,
        string SubjectType, string SubjectReference, DateTimeOffset ExpiresAt, string Code);
}
