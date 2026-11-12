using ARGOS.SharedKernel.Abstractions;

namespace ARGOS.Domain.Aggregates.CaseFile.Events;

/// <summary>
/// Evento de dominio que se publica cuando un expediente es creado.
/// </summary>
/// <param name="CaseFileId">Identificador del expediente.</param>
/// <param name="Number">Número institucional del expediente.</param>
public sealed record CaseFileCreatedDomainEvent(
    Guid CaseFileId,
    string Number) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}