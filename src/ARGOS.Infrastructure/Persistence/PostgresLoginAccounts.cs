using ARGOS.Application.Security;
using ARGOS.Application.Common.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ARGOS.Infrastructure.Persistence;

public sealed class PostgresLoginAccounts(IServiceScopeFactory scopes, TimeProvider clock) : ILoginAccounts, IPasswordChanger
{
    public async Task<LoginAccount?> ValidatePasswordAsync(string identification, string password, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(identification) || identification.Length > 100
            || string.IsNullOrEmpty(password) || password.Length > 1024) return null;
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
        // Official identity is the only login identifier. Resolve existing accounts by
        // person, preserving their password, designation and persistent lockout state.
        // Ambiguous document numbers fail closed; never select an arbitrary account.
        var documentNumber = identification.Trim();
        var matches = await db.Database.SqlQuery<Guid>($"""
            SELECT a."PersonId" AS "Value"
            FROM "AccessAccounts" a
            JOIN "PersonnelPeople" p ON p."Id" = a."PersonId"
            WHERE p."Identity"::jsonb ->> 'Number' = {documentNumber}
            """).Take(2).ToListAsync(ct);
        if (matches.Count != 1) return null;
        var matchedPerson = matches[0];
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Serialize attempts across requests and server instances so failed attempts
        // cannot overwrite each other and bypass the persistent lockout threshold.
        var account = await db.Accounts.FromSqlInterpolated(
            $"SELECT * FROM \"AccessAccounts\" WHERE \"PersonId\" = {matchedPerson} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        var now = clock.GetUtcNow();
        if (account is null || !account.Enabled || account.LockedUntilTicks > now.UtcTicks) return null;
        if (!account.VerifyPassword(password))
        {
            account.FailedAttempts++;
            if (account.FailedAttempts >= 5)
            {
                account.LockedUntilTicks = now.AddMinutes(15).UtcTicks;
                account.FailedAttempts = 0;
            }
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return null;
        }
        account.FailedAttempts = 0;
        account.LockedUntilTicks = 0;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await GetActiveAsync(account.PersonId, ct);
    }

    public async Task<LoginAccount?> GetActiveAsync(Guid personId, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
        var account = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(a => a.PersonId == personId, ct);
        var now = clock.GetUtcNow();
        if (account is null || !account.Enabled || account.LockedUntilTicks > now.UtcTicks) return null;
        var personnel = scope.ServiceProvider.GetRequiredService<PersonnelDbContext>();
        var person = await personnel.People.AsNoTracking().SingleOrDefaultAsync(p => p.Id == personId, ct);
        if (person is null) return null;
        var setup = await scope.ServiceProvider.GetRequiredService<IOrganizationSetupRepository>()
            .GetPublishedAsync(person.OrganizationId, ct);
        if (setup is null) return null;
        var available = setup.Positions.Select(p => p.Id).ToHashSet();
        var positions = person.Assignments.Where(a => a.IsEffectiveAt(now) && available.Contains(a.PositionId))
            .Select(a => a.PositionId).ToArray();
        if (positions.Length == 0) return null;
        var permissions = await db.PositionPermissions.AsNoTracking()
            .Where(p => p.OrganizationId == person.OrganizationId.Value && positions.Contains(p.PositionId))
            .Select(p => p.Permission).Distinct().ToArrayAsync(ct);
        if (permissions.Contains("personnel.manage"))
        {
            var administrativePositions = await db.PositionPermissions.AsNoTracking()
                .Where(p => p.OrganizationId == person.OrganizationId.Value && p.Permission == "personnel.manage")
                .Select(p => p.PositionId).ToArrayAsync(ct);
            var designations = await scope.ServiceProvider.GetRequiredService<DesignationDbContext>()
                .Designations.AsNoTracking().Where(d => d.OrganizationId == person.OrganizationId.Value
                    && d.PersonId == person.Id).ToListAsync(ct);
            var effectiveAssignments = person.Assignments.Where(a => a.IsEffectiveAt(now)
                && positions.Contains(a.PositionId) && administrativePositions.Contains(a.PositionId)).Select(a => a.Id).ToHashSet();
            if (!designations.Any(d => d.IsEffectiveAt(now) && effectiveAssignments.Contains(d.AssignmentId)))
                permissions = permissions.Where(p => p != "personnel.manage").ToArray();
        }
        return new LoginAccount(person.Id, person.OrganizationId.Value, person.FullName,
            account.MaskedDestination, account.Channel, permissions, account.MustChangePassword, Convert.ToBase64String(account.Salt));
    }

    public async Task<bool> ChangePasswordAsync(Guid personId, string currentPassword, string newPassword, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var account = await db.Accounts.FromSqlInterpolated(
            $"SELECT * FROM \"AccessAccounts\" WHERE \"PersonId\" = {personId} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (account is null || !account.Enabled || account.LockedUntilTicks > clock.GetUtcNow().UtcTicks
            || !account.VerifyPassword(currentPassword)) return false;
        account.ChangePassword(newPassword);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }
}
