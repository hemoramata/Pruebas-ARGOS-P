using System.Security.Claims;
using ARGOS.Domain.Aggregates.Organization.ValueObjects;
using ARGOS.Domain.Aggregates.Personnel;
using ARGOS.Application.Common.Persistence;
using ARGOS.Infrastructure.Persistence;
using Npgsql;
using Microsoft.EntityFrameworkCore;

namespace ARGOS.API.Endpoints;

public static class PersonnelEndpoints
{
    public static IEndpointRouteBuilder MapPersonnelEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/personnel").RequireAuthorization("PersonnelAdministration");
        group.AddEndpointFilter(async (context, next) =>
        {
            if (!Guid.TryParse(context.HttpContext.User.FindFirstValue("organization_id"), out var organization)
                || organization == Guid.Empty)
                return Results.Forbid();
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            try { return await next(context); }
            catch (ArgumentException) { return Results.BadRequest(new { code = "Personnel.InvalidInput" }); }
            catch (InvalidOperationException) { return Results.Conflict(new { code = "Personnel.InvalidState" }); }
            catch (DbUpdateConcurrencyException) { return Results.Conflict(new { code = "Personnel.ConcurrentUpdate" }); }
            catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            { return Results.Conflict(new { code = "Personnel.DuplicateOrInvalidReference" }); }
        });

        group.MapPost("/", async (CreatePerson request, HttpContext http, PersonnelDbContext db,
            IOrganizationSetupRepository organizations, CancellationToken ct) =>
        {
            var organization = OrganizationId.From(Guid.Parse(http.User.FindFirstValue("organization_id")!));
            if (await organizations.GetPublishedAsync(organization, ct) is null)
                return Results.Conflict(new { code = "Personnel.PublishedOrganizationRequired" });
            var person = new InstitutionalPerson(organization,
                new OfficialIdentity(request.Issuer, request.DocumentType, request.Number), request.FullName);
            db.People.Add(person);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/personnel/{person.Id}", new { person.Id, person.FullName });
        });

        group.MapGet("/", async (HttpContext http, PersonnelDbContext db, CancellationToken ct) =>
        {
            var organization = OrganizationId.From(Guid.Parse(http.User.FindFirstValue("organization_id")!));
            var people = await db.People.AsNoTracking()
                .Where(person => person.OrganizationId == organization)
                .OrderBy(person => person.FullName)
                .Select(person => new
                {
                    person.Id,
                    person.FullName,
                    Issuer = person.Identity.Issuer,
                    DocumentType = person.Identity.DocumentType,
                    Number = person.Identity.Number,
                    ActiveAssignments = person.Assignments.Count(assignment => assignment.RevokedAt == null
                        && (assignment.EndsAt == null || assignment.EndsAt > DateTimeOffset.UtcNow))
                })
                .ToListAsync(ct);
            return Results.Ok(people);
        });

        group.MapGet("/{id:guid}", async (Guid id, HttpContext http, PersonnelDbContext db, CancellationToken ct) =>
        {
            var organization = OrganizationId.From(Guid.Parse(http.User.FindFirstValue("organization_id")!));
            var person = await db.People.AsNoTracking().SingleOrDefaultAsync(
                p => p.Id == id && p.OrganizationId == organization, ct);
            return person is null ? Results.NotFound() : Results.Ok(new
            {
                person.Id, person.FullName,
                Assignments = person.Assignments.Select(a => new { a.Id, a.PositionId, a.StartsAt, a.EndsAt, a.RevokedAt })
            });
        });

        group.MapPost("/{id:guid}/assignments", async (Guid id, AssignPosition request, HttpContext http,
            PersonnelDbContext db, IOrganizationSetupRepository organizations, CancellationToken ct) =>
        {
            var organization = OrganizationId.From(Guid.Parse(http.User.FindFirstValue("organization_id")!));
            var person = await db.People.SingleOrDefaultAsync(p => p.Id == id && p.OrganizationId == organization, ct);
            if (person is null) return Results.NotFound();
            var setup = await organizations.GetPublishedAsync(organization, ct);
            if (setup is null) return Results.Conflict(new { code = "Personnel.PublishedOrganizationRequired" });
            var assignment = person.Assign(setup, request.PositionId, request.StartsAt, request.EndsAt,
                $"session:{http.User.FindFirstValue(ClaimTypes.NameIdentifier)};request:{http.TraceIdentifier}");
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { assignment.Id, assignment.PositionId, assignment.StartsAt, assignment.EndsAt });
        });

        group.MapPost("/{id:guid}/assignments/{assignmentId:guid}/revoke", async (Guid id, Guid assignmentId,
            HttpContext http, PersonnelDbContext db, CancellationToken ct) =>
        {
            var organization = OrganizationId.From(Guid.Parse(http.User.FindFirstValue("organization_id")!));
            var person = await db.People.SingleOrDefaultAsync(p => p.Id == id && p.OrganizationId == organization, ct);
            if (person is null) return Results.NotFound();
            person.RevokeAssignment(assignmentId, DateTimeOffset.UtcNow,
                $"session:{http.User.FindFirstValue(ClaimTypes.NameIdentifier)};request:{http.TraceIdentifier}");
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
        group.MapPost("/{id:guid}/administrator-designations", async (Guid id, DesignateAdministrator request,
            HttpContext http, PersonnelDbContext personnel, DesignationDbContext db,
            IOrganizationSetupRepository organizations, CancellationToken ct) =>
        {
            var organization = OrganizationId.From(Guid.Parse(http.User.FindFirstValue("organization_id")!));
            var person = await personnel.People.SingleOrDefaultAsync(p => p.Id == id && p.OrganizationId == organization, ct);
            if (person is null) return Results.NotFound();
            var setup = await organizations.GetPublishedAsync(organization, ct);
            var assignment = person.Assignments.SingleOrDefault(a => a.Id == request.AssignmentId);
            if (setup is null || assignment is null || !setup.Positions.Any(p => p.Id == assignment.PositionId))
                return Results.Conflict(new { code = "Personnel.CurrentPositionRequired" });
            var designation = AdministratorDesignation.Create(person, request.AssignmentId,
                request.AuthorizationReference, request.AuthorizedBy,
                http.User.FindFirstValue(ClaimTypes.NameIdentifier)!, DateTimeOffset.UtcNow,
                request.StartsAt, request.EndsAt);
            db.Designations.Add(designation);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/personnel/{id}/administrator-designations/{designation.Id}", designation);
        });

        group.MapGet("/{id:guid}/administrator-designations", async (Guid id, HttpContext http,
            DesignationDbContext db, CancellationToken ct) =>
        {
            var organization = Guid.Parse(http.User.FindFirstValue("organization_id")!);
            return Results.Ok(await db.Designations.AsNoTracking()
                .Where(d => d.OrganizationId == organization && d.PersonId == id).ToListAsync(ct));
        });

        group.MapPost("/{id:guid}/administrator-designations/{designationId:guid}/revoke", async (
            Guid id, Guid designationId, RevokeDesignation request, HttpContext http,
            DesignationDbContext db, CancellationToken ct) =>
        {
            var organization = Guid.Parse(http.User.FindFirstValue("organization_id")!);
            var designation = await db.Designations.SingleOrDefaultAsync(d => d.Id == designationId
                && d.PersonId == id && d.OrganizationId == organization, ct);
            if (designation is null) return Results.NotFound();
            designation.Revoke(DateTimeOffset.UtcNow, request.AuthorizationReference);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
        return endpoints;
    }

    public sealed record CreatePerson(string Issuer, string DocumentType, string Number, string FullName);
    public sealed record AssignPosition(Guid PositionId, DateTimeOffset StartsAt, DateTimeOffset? EndsAt);
    public sealed record DesignateAdministrator(Guid AssignmentId, string AuthorizationReference,
        string AuthorizedBy, DateTimeOffset StartsAt, DateTimeOffset? EndsAt);
    public sealed record RevokeDesignation(string AuthorizationReference);
}
