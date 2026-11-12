namespace ARGOS.SharedKernel.Abstractions;

/// <summary>
/// Representa la clase base para todos los Aggregate Roots del dominio.
///
/// Un Aggregate Root es el único punto de entrada autorizado para modificar
/// el estado interno de un agregado y es responsable de registrar los
/// Domain Events que serán publicados al finalizar la transacción.
/// </summary>
/// <typeparam name="TId">Tipo del identificador de la entidad.</typeparam>
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull
{
    #region Fields

    private readonly List<IDomainEvent> _domainEvents = new();

    #endregion

    #region Constructors

    protected AggregateRoot()
    {
    }

    protected AggregateRoot(TId id)
        : base(id)
    {
    }

    #endregion

    #region Domain Events

    /// <summary>
    /// Obtiene la colección de eventos de dominio pendientes de publicación.
    /// </summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents =>
        _domainEvents.AsReadOnly();

    /// <summary>
    /// Indica si el agregado posee eventos pendientes.
    /// </summary>
    public bool HasDomainEvents => _domainEvents.Count > 0;

    /// <summary>
    /// Registra un nuevo evento de dominio.
    /// </summary>
    /// <param name="domainEvent">Evento a registrar.</param>
    protected void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        _domainEvents.Add(domainEvent);
    }

    /// <summary>
    /// Mantenido por compatibilidad con versiones anteriores.
    /// Se recomienda utilizar RaiseDomainEvent().
    /// </summary>
    public void AddDomainEvent(IDomainEvent domainEvent)
    {
        RaiseDomainEvent(domainEvent);
    }

    /// <summary>
    /// Elimina un evento específico.
    /// </summary>
    public void RemoveDomainEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        _domainEvents.Remove(domainEvent);
    }

    /// <summary>
    /// Elimina todos los eventos registrados.
    /// </summary>
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    #endregion
}