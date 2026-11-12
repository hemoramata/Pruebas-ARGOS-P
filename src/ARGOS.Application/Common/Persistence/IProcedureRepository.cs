using ARGOS.Domain.Aggregates.Procedure;
using ARGOS.Domain.Aggregates.Procedure.ValueObjects;

namespace ARGOS.Application.Common.Persistence;

public interface IProcedureRepository
{
    Task<IReadOnlyCollection<Procedure>> ListPublishedAsync(
        CancellationToken cancellationToken = default);

    Task<Procedure?> GetPublishedAsync(
        ProcedureDefinitionId definitionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Procedure>> ListVersionsAsync(
        ProcedureDefinitionId definitionId,
        CancellationToken cancellationToken = default);

    Task<Procedure?> GetByIdAsync(
        ProcedureId id,
        CancellationToken cancellationToken = default);

    Task<bool> TryAddAsync(
        Procedure procedure,
        CancellationToken cancellationToken = default);

    Task<bool> TryAddVersionAsync(
        Procedure supersededVersion,
        Procedure newVersion,
        CancellationToken cancellationToken = default);

    Task<bool> TrySaveChangesAsync(
        CancellationToken cancellationToken = default);
}
