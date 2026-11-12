using ARGOS.Domain.Aggregates.Procedure.ValueObjects;
using ARGOS.SharedKernel.Abstractions;

namespace ARGOS.Domain.Aggregates.Procedure.Events;

/// <summary>
/// Raised when a new Procedure is created.
/// </summary>
public sealed class ProcedureCreatedEvent : IDomainEvent
{
    public ProcedureCreatedEvent(ProcedureId procedureId)
    {
        ProcedureId = procedureId;
        OccurredOn = DateTime.UtcNow;
    }

    /// <summary>
    /// Procedure identifier.
    /// </summary>
    public ProcedureId ProcedureId { get; }

    /// <inheritdoc />
    public DateTime OccurredOn { get; }
}