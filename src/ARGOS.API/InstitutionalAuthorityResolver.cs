using ARGOS.Application.Common.Persistence;
using ARGOS.Domain.Aggregates.Organization.ValueObjects;
using ARGOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ARGOS.API;

public sealed record ResolvedAuthority(Guid Person, Guid Organization, Guid Assignment, Guid Position,
    string Competence, Guid Setup, int SetupVersion, string AppointmentAuthorization);

public sealed class InstitutionalAuthorityResolver(PersonnelDbContext personnel, AccessDbContext access,
    IOrganizationSetupRepository organizations, TimeProvider clock, ArgosDbContext main, DesignationDbContext designations)
{
    public async Task<ResolvedAuthority?> ResolveAsync(Guid personId, Guid organization, Guid assignmentId,
        string competenceCode, string permission, CancellationToken ct)
    {
        if (main.Database.CurrentTransaction is not null)
        {
            // All contexts use the same institutional database. Hold locks in the
            // decision transaction while reading current authority through the other contexts.
            await main.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"PersonnelPeople\" WHERE \"Id\" = {personId} FOR SHARE", ct);
            await main.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"PersonnelAssignments\" WHERE \"Id\" = {assignmentId} FOR SHARE", ct);
            await main.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"OrganizationSetups\" WHERE \"OrganizationId\" = {organization} AND \"Status\" = 'Published' FOR SHARE", ct);
            await main.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"AccessPositionPermissions\" WHERE \"OrganizationId\" = {organization} AND \"Permission\" = {permission} FOR SHARE", ct);
        }
        var org = OrganizationId.From(organization);
        var person = await personnel.People.AsNoTracking().SingleOrDefaultAsync(x => x.Id == personId && x.OrganizationId == org, ct);
        var assignment = person?.Assignments.SingleOrDefault(x => x.Id == assignmentId && x.IsEffectiveAt(clock.GetUtcNow()));
        if (assignment is null) return null;
        if (permission == "personnel.manage")
        {
            if (main.Database.CurrentTransaction is not null)
                await main.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"AdministratorDesignations\" WHERE \"AssignmentId\" = {assignmentId} FOR SHARE", ct);
            var appointments = await designations.Designations.AsNoTracking().Where(x => x.OrganizationId == organization
                && x.PersonId == personId && x.AssignmentId == assignmentId).ToListAsync(ct);
            if (!appointments.Any(x => x.IsEffectiveAt(clock.GetUtcNow()))) return null;
        }
        var setup = await organizations.GetPublishedAsync(org, ct);
        var position = setup?.Positions.SingleOrDefault(x => x.Id == assignment.PositionId);
        var competence = setup?.Competences.SingleOrDefault(x => x.Code == competenceCode);
        if (position is null || competence is null || !position.CompetenceIds.Contains(competence.Id)
            || !await access.PositionPermissions.AnyAsync(x => x.OrganizationId == organization
                && x.PositionId == position.Id && x.Permission == permission, ct)) return null;
        return new(personId, organization, assignment.Id, position.Id, competence.Code,
            setup!.Id.Value, setup.Version, assignment.AuthorizationReference);
    }
}
