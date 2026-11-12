using System.Security.Claims;
using ARGOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ARGOS.API.Endpoints;

/// <summary>Read-only institutional activity log for authorized administrators.</summary>
public static class AuditEndpoints
{
    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/audit-entries").RequireAuthorization("PersonnelAdministration");

        group.MapGet("/", async (HttpContext http, ArgosDbContext db, int? limit, CancellationToken ct) =>
        {
            if (!Guid.TryParse(http.User.FindFirstValue("organization_id"), out var organization) || organization == Guid.Empty)
                return Results.Forbid();

            var take = Math.Clamp(limit ?? 100, 1, 200);
            var entries = await db.AuditEntries.AsNoTracking()
                .Where(entry => entry.OrganizationId == organization)
                .OrderByDescending(entry => entry.OccurredAtUtc)
                .Take(take)
                .Select(entry => new
                {
                    entry.OccurredAtUtc,
                    entry.ActorReference,
                    entry.ActionCode,
                    entry.SubjectType,
                    entry.SubjectReference,
                    entry.Outcome,
                    entry.Details
                })
                .ToListAsync(ct);

            http.Response.Headers.CacheControl = "no-store";
            return Results.Ok(entries);
        });

        return endpoints;
    }
}
