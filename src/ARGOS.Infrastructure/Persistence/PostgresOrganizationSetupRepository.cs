using ARGOS.Application.Common.Persistence;
using ARGOS.Domain.Aggregates.Organization;
using ARGOS.Domain.Aggregates.Organization.ValueObjects;
using ARGOS.Domain.Aggregates.OrganizationSetup;
using ARGOS.Domain.Aggregates.OrganizationSetup.Enums;
using ARGOS.Domain.Aggregates.OrganizationSetup.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ARGOS.Infrastructure.Persistence;

public sealed class PostgresOrganizationSetupRepository : IOrganizationSetupRepository
{
    private readonly ArgosDbContext _dbContext;

    public PostgresOrganizationSetupRepository(ArgosDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<OrganizationSetup?> GetCurrentAsync(
        CancellationToken cancellationToken = default)
        => _dbContext.OrganizationSetups
            .OrderByDescending(setup => setup.Version)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<Organization?> GetOrganizationAsync(
        OrganizationId id,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        return _dbContext.Organizations.SingleOrDefaultAsync(
            organization => organization.Id == id,
            cancellationToken);
    }

    public Task<OrganizationSetup?> GetByIdAsync(
        OrganizationSetupId id,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        return _dbContext.OrganizationSetups.SingleOrDefaultAsync(
            setup => setup.Id == id,
            cancellationToken);
    }

    public Task<OrganizationSetup?> GetPublishedAsync(
        OrganizationId organizationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(organizationId);
        return _dbContext.OrganizationSetups.SingleOrDefaultAsync(
            setup => setup.OrganizationId == organizationId
                && setup.Status == OrganizationSetupStatus.Published,
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<OrganizationSetup>> ListVersionsAsync(
        OrganizationId organizationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(organizationId);
        return await _dbContext.OrganizationSetups
            .AsNoTracking()
            .Where(setup => setup.OrganizationId == organizationId)
            .OrderBy(setup => setup.Version)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<bool> TryAddInitialAsync(
        Organization organization,
        OrganizationSetup setup,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(organization);
        ArgumentNullException.ThrowIfNull(setup);

        await using var transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        if (await _dbContext.Organizations.AnyAsync(cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        _dbContext.Organizations.Add(organization);
        _dbContext.OrganizationSetups.Add(setup);

        return await TryCommitAsync(transaction, cancellationToken);
    }

    public async Task<bool> TryAddVersionAsync(
        OrganizationSetup currentVersion,
        OrganizationSetup newVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(currentVersion);
        ArgumentNullException.ThrowIfNull(newVersion);

        await using var transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        _dbContext.OrganizationSetups.Add(newVersion);
        return await TryCommitAsync(transaction, cancellationToken);
    }

    public async Task<bool> TrySaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (IsConstraintViolation(exception))
        {
            return false;
        }
    }

    private async Task<bool> TryCommitAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (IsConstraintViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }
    }

    private static bool IsConstraintViolation(DbUpdateException exception)
        => exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
