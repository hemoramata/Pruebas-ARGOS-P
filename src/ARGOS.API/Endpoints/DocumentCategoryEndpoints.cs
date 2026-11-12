using System.Security.Claims;
using ARGOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ARGOS.API.Endpoints;

/// <summary>Institution-owned categories used by capture clients as reviewable suggestions.</summary>
public static class DocumentCategoryEndpoints
{
    public sealed record CreateCategory(string Code, string Name, string? Description);

    public static IEndpointRouteBuilder MapDocumentCategoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var read = endpoints.MapGroup("/api/document-categories").RequireAuthorization("evidences.create");
        read.AddEndpointFilter(async (context, next) =>
        {
            if (!Guid.TryParse(context.HttpContext.User.FindFirstValue("organization_id"), out var org) || org == Guid.Empty) return Results.Forbid();
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
        read.MapGet("/", async (HttpContext http, ArgosDbContext db, CancellationToken ct) =>
        {
            var org = Guid.Parse(http.User.FindFirstValue("organization_id")!);
            return Results.Ok(await db.DocumentCategories.AsNoTracking().Where(x => x.OrganizationId == org && x.IsActive)
                .OrderBy(x => x.Name).Select(x => new { x.Id, x.Code, x.Name, x.Description }).ToListAsync(ct));
        });

        var manage = endpoints.MapGroup("/api/document-categories").RequireAuthorization("PersonnelAdministration");
        manage.MapPost("/", async (CreateCategory request, HttpContext http, ArgosDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var org = Guid.Parse(http.User.FindFirstValue("organization_id")!);
            try
            {
                var category = DocumentCategory.Create(org, request.Code, request.Name, request.Description, clock.GetUtcNow());
                if (await db.DocumentCategories.AnyAsync(x => x.OrganizationId == org && x.Code == category.Code, ct))
                    return Results.Conflict(new { code = "DocumentCategory.DuplicateCode" });
                db.DocumentCategories.Add(category);
                db.AuditEntries.Add(Audit(http, org, "DocumentCategory.Created", category.Id, "Succeeded", category.Code));
                await db.SaveChangesAsync(ct);
                return Results.Created($"/api/document-categories/{category.Id}", new { category.Id, category.Code, category.Name, category.Description });
            }
            catch (ArgumentException error) { return Results.BadRequest(new { code = "DocumentCategory.Invalid", detail = error.Message }); }
        });
        manage.MapPost("/{id:guid}/deactivate", async (Guid id, HttpContext http, ArgosDbContext db, CancellationToken ct) =>
        {
            var org = Guid.Parse(http.User.FindFirstValue("organization_id")!);
            var category = await db.DocumentCategories.SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == org, ct);
            if (category is null) return Results.NotFound();
            category.Deactivate();
            db.AuditEntries.Add(Audit(http, org, "DocumentCategory.Deactivated", id, "Succeeded", category.Code));
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
        return endpoints;
    }

    private static AuditEntry Audit(HttpContext http, Guid org, string action, Guid subject, string outcome, string detail) =>
        AuditEntry.Create(org, http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown", http.User.FindFirstValue("session_id"), action,
            "DocumentCategory", subject.ToString(), outcome, http.TraceIdentifier, detail);
}
