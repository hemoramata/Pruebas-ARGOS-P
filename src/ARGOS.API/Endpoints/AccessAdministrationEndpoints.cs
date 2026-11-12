using System.Net.Mail;
using System.Security.Claims;
using ARGOS.Application.Common.Persistence;
using ARGOS.Domain.Aggregates.Organization.ValueObjects;
using ARGOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ARGOS.API.Endpoints;

/// <summary>Institutional account provisioning and position-based permissions.
/// Access is deliberately granted to positions, never directly to an individual.
/// </summary>
public static class AccessAdministrationEndpoints
{
    private static readonly string[] PermissionCatalog =
    [
        "personnel.manage", "procedures.manage", "case-files.read", "case-files.create",
        "case-files.update", "case-files.finalize", "case-files.archive", "evidences.read", "evidences.create"
    ];

    public static IEndpointRouteBuilder MapAccessAdministrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/access-administration").RequireAuthorization("PersonnelAdministration");
        group.AddEndpointFilter(async (context, next) =>
        {
            if (!Guid.TryParse(context.HttpContext.User.FindFirstValue("organization_id"), out var organization)
                || organization == Guid.Empty) return Results.Forbid();
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            try { return await next(context); }
            catch (ArgumentException error) { return Results.BadRequest(new { code = "AccessAdministration.InvalidInput", detail = error.Message }); }
            catch (InvalidOperationException error) { return Results.Conflict(new { code = "AccessAdministration.InvalidState", detail = error.Message }); }
        });

        group.MapGet("/accounts", async (HttpContext http, AccessDbContext access, PersonnelDbContext personnel, CancellationToken ct) =>
        {
            var organization = OrganizationId.From(Guid.Parse(http.User.FindFirstValue("organization_id")!));
            var people = await personnel.People.AsNoTracking().Where(p => p.OrganizationId == organization)
                .Select(p => new { p.Id, p.FullName, Identification = p.Identity.Number }).ToListAsync(ct);
            var accounts = await access.Accounts.AsNoTracking().ToListAsync(ct);
            return Results.Ok(from account in accounts
                join person in people on account.PersonId equals person.Id
                orderby person.FullName
                select new { person.Id, person.FullName, person.Identification, account.Enabled, account.MustChangePassword,
                    account.MaskedDestination });
        });

        group.MapPost("/accounts", async (ProvisionAccount request, HttpContext http, AccessDbContext access,
            PersonnelDbContext personnel, IOrganizationSetupRepository organizations, TimeProvider clock, CancellationToken ct) =>
        {
            var organization = OrganizationId.From(Guid.Parse(http.User.FindFirstValue("organization_id")!));
            var person = await personnel.People
                .Include(p => p.Assignments)
                .SingleOrDefaultAsync(p => p.Id == request.PersonId && p.OrganizationId == organization, ct);
            if (person is null) return Results.NotFound();
            var setup = await organizations.GetPublishedAsync(organization, ct);
            if (setup is null || !person.Assignments.Any(a => a.IsEffectiveAt(clock.GetUtcNow()) && setup.Positions.Any(p => p.Id == a.PositionId)))
                return Results.Conflict(new { code = "AccessAdministration.CurrentAssignmentRequired" });
            if (!MailAddress.TryCreate(request.Email, out var email) || email.Address != request.Email.Trim())
                throw new ArgumentException("Indique un correo institucional válido.");
            AccessAccount.ValidatePasswordPolicy(request.TemporaryPassword);
            if (await access.Accounts.AnyAsync(a => a.PersonId == person.Id, ct))
                return Results.Conflict(new { code = "AccessAdministration.AccountAlreadyExists" });
            var account = AccessAccount.Provision(person.Id, person.Identity.Number, request.TemporaryPassword,
                ARGOS.Application.Security.VerificationChannel.InstitutionalEmail, email.Address, "email-challenge:institutional-provisioning");
            access.Accounts.Add(account);
            await access.SaveChangesAsync(ct);
            return Results.Created($"/api/access-administration/accounts/{person.Id}", new { person.Id, person.FullName, account.MaskedDestination });
        });

        group.MapPost("/accounts/{personId:guid}/disable", async (Guid personId, HttpContext http, AccessDbContext access,
            PersonnelDbContext personnel, CancellationToken ct) =>
        {
            var organization = OrganizationId.From(Guid.Parse(http.User.FindFirstValue("organization_id")!));
            if (!await personnel.People.AnyAsync(p => p.Id == personId && p.OrganizationId == organization, ct)) return Results.NotFound();
            var account = await access.Accounts.SingleOrDefaultAsync(a => a.PersonId == personId, ct);
            if (account is null) return Results.NotFound();
            account.Disable();
            await access.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        group.MapPost("/accounts/{personId:guid}/enable", async (Guid personId, HttpContext http, AccessDbContext access,
            PersonnelDbContext personnel, CancellationToken ct) =>
        {
            var organization = OrganizationId.From(Guid.Parse(http.User.FindFirstValue("organization_id")!));
            if (!await personnel.People.AnyAsync(p => p.Id == personId && p.OrganizationId == organization, ct)) return Results.NotFound();
            var account = await access.Accounts.SingleOrDefaultAsync(a => a.PersonId == personId, ct);
            if (account is null) return Results.NotFound();
            account.Enable();
            await access.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        group.MapGet("/permissions", async (HttpContext http, AccessDbContext access, IOrganizationSetupRepository organizations, CancellationToken ct) =>
        {
            var organization = OrganizationId.From(Guid.Parse(http.User.FindFirstValue("organization_id")!));
            var setup = await organizations.GetPublishedAsync(organization, ct);
            if (setup is null) return Results.Conflict(new { code = "AccessAdministration.PublishedOrganizationRequired" });
            var granted = await access.PositionPermissions.AsNoTracking().Where(p => p.OrganizationId == organization.Value).ToListAsync(ct);
            return Results.Ok(new { Permissions = PermissionCatalog, Positions = setup.Positions.OrderBy(p => p.Code).Select(position => new
            {
                position.Id, position.Code, position.Name,
                Permissions = granted.Where(permission => permission.PositionId == position.Id).Select(permission => permission.Permission).Order().ToArray()
            }) });
        });

        group.MapPut("/permissions/{positionId:guid}", async (Guid positionId, SetPositionPermissions request, HttpContext http,
            AccessDbContext access, IOrganizationSetupRepository organizations, CancellationToken ct) =>
        {
            var organization = OrganizationId.From(Guid.Parse(http.User.FindFirstValue("organization_id")!));
            var setup = await organizations.GetPublishedAsync(organization, ct);
            if (setup is null || !setup.Positions.Any(position => position.Id == positionId)) return Results.NotFound();
            var requested = (request.Permissions ?? []).Distinct(StringComparer.Ordinal).ToArray();
            if (requested.Any(permission => !PermissionCatalog.Contains(permission, StringComparer.Ordinal)))
                throw new ArgumentException("La selección contiene un permiso no reconocido.");
            var existing = await access.PositionPermissions.Where(p => p.OrganizationId == organization.Value && p.PositionId == positionId).ToListAsync(ct);
            access.PositionPermissions.RemoveRange(existing);
            access.PositionPermissions.AddRange(requested.Select(permission => new PositionPermission
            { OrganizationId = organization.Value, PositionId = positionId, Permission = permission }));
            await access.SaveChangesAsync(ct);
            return Results.NoContent();
        });
        return endpoints;
    }

    public sealed record ProvisionAccount(Guid PersonId, string Email, string TemporaryPassword);
    public sealed record SetPositionPermissions(string[]? Permissions);
}
