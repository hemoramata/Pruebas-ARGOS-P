using ARGOS.Application.Common.Persistence;
using ARGOS.Domain.Aggregates.Procedure;
using ARGOS.Domain.Aggregates.Procedure.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ARGOS.Infrastructure.Persistence;

public sealed class PostgresProcedureRepository : IProcedureRepository
{
    private readonly ArgosDbContext _dbContext;

    public PostgresProcedureRepository(ArgosDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<Procedure>> ListPublishedAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Procedures
            .AsNoTracking()
            .Where(procedure => procedure.Status == Domain.Aggregates.Procedure.Enums.ProcedureStatus.Published)
            .OrderBy(procedure => procedure.Code)
            .ToArrayAsync(cancellationToken);
    }

    public Task<Procedure?> GetPublishedAsync(
        ProcedureDefinitionId definitionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definitionId);

        return _dbContext.Procedures.SingleOrDefaultAsync(
            procedure => procedure.DefinitionId == definitionId
                && procedure.Status == Domain.Aggregates.Procedure.Enums.ProcedureStatus.Published,
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<Procedure>> ListVersionsAsync(
        ProcedureDefinitionId definitionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definitionId);

        return await _dbContext.Procedures
            .AsNoTracking()
            .Where(procedure => procedure.DefinitionId == definitionId)
            .OrderBy(procedure => procedure.Version)
            .ToArrayAsync(cancellationToken);
    }

    public Task<Procedure?> GetByIdAsync(
        ProcedureId id,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return _dbContext.Procedures
            .SingleOrDefaultAsync(
                procedure => procedure.Id == id,
                cancellationToken);
    }

    public async Task<bool> TryAddVersionAsync(
        Procedure supersededVersion,
        Procedure newVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(supersededVersion);
        ArgumentNullException.ThrowIfNull(newVersion);

        await using var transaction = _dbContext.Database.CurrentTransaction is null
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken) : null;

        _dbContext.Procedures.Add(newVersion);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
            return false;
        }
    }

    public async Task<bool> TryAddAsync(
        Procedure procedure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(procedure);

        var duplicateCode = await _dbContext.Procedures.AnyAsync(
            existing => existing.Code == procedure.Code,
            cancellationToken);

        if (duplicateCode)
        {
            return false;
        }

        _dbContext.Procedures.Add(procedure);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return false;
        }
    }

    public async Task<bool> TrySaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return false;
        }
    }
}
