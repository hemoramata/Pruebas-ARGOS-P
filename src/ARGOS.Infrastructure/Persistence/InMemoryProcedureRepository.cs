using ARGOS.Application.Common.Persistence;
using ARGOS.Domain.Aggregates.Procedure;
using ARGOS.Domain.Aggregates.Procedure.ValueObjects;

namespace ARGOS.Infrastructure.Persistence;

public sealed class InMemoryProcedureRepository : IProcedureRepository
{
    private readonly Dictionary<Guid, Procedure> _procedures = new();
    private readonly object _sync = new();

    public Task<IReadOnlyCollection<Procedure>> ListPublishedAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            IReadOnlyCollection<Procedure> result = _procedures.Values
                .Where(procedure => procedure.Status == Domain.Aggregates.Procedure.Enums.ProcedureStatus.Published)
                .OrderBy(procedure => procedure.Code, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return Task.FromResult(result);
        }
    }

    public Task<Procedure?> GetPublishedAsync(
        ProcedureDefinitionId definitionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definitionId);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            var result = _procedures.Values.SingleOrDefault(procedure =>
                procedure.DefinitionId == definitionId
                && procedure.Status == Domain.Aggregates.Procedure.Enums.ProcedureStatus.Published);

            return Task.FromResult(result);
        }
    }

    public Task<IReadOnlyCollection<Procedure>> ListVersionsAsync(
        ProcedureDefinitionId definitionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definitionId);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            IReadOnlyCollection<Procedure> result = _procedures.Values
                .Where(procedure => procedure.DefinitionId == definitionId)
                .OrderBy(procedure => procedure.Version)
                .ToArray();

            return Task.FromResult(result);
        }
    }

    public Task<Procedure?> GetByIdAsync(
        ProcedureId id,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            _procedures.TryGetValue(id.Value, out var procedure);
            return Task.FromResult(procedure);
        }
    }

    public Task<bool> TryAddVersionAsync(
        Procedure supersededVersion,
        Procedure newVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(supersededVersion);
        ArgumentNullException.ThrowIfNull(newVersion);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            var conflict = _procedures.ContainsKey(newVersion.Id.Value)
                || _procedures.Values.Any(existing =>
                    existing.DefinitionId == newVersion.DefinitionId
                    && existing.Version == newVersion.Version);

            if (conflict)
                return Task.FromResult(false);

            _procedures.Add(newVersion.Id.Value, newVersion);
            return Task.FromResult(true);
        }
    }

    public Task<bool> TryAddAsync(
        Procedure procedure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(procedure);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            var duplicateCode = _procedures.Values.Any(existing =>
                string.Equals(
                    existing.Code,
                    procedure.Code,
                    StringComparison.OrdinalIgnoreCase));

            if (duplicateCode)
            {
                return Task.FromResult(false);
            }

            _procedures.Add(procedure.Id.Value, procedure);
            return Task.FromResult(true);
        }
    }

    public Task<bool> TrySaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(true);
    }
}
