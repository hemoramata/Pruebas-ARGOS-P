using ARGOS.Domain.ValueObjects;
using ARGOS.SharedKernel.Abstractions;

namespace ARGOS.Domain.Entities;

/// <summary>
/// Representa cualquier participante dentro del dominio de ARGOS.
/// Puede tratarse de una persona física, una organización,
/// una institución pública o cualquier entidad que participe
/// en uno o varios expedientes.
/// </summary>
public abstract class Party : Entity<Guid>
{
    protected Party()
    {
    }

    protected Party(
        Guid id,
        string displayName,
        TaxIdentifier taxIdentifier,
        Email? email,
        PhoneNumber? phoneNumber)
        : base(id)
    {
        DisplayName = displayName;
        TaxIdentifier = taxIdentifier;
        Email = email;
        PhoneNumber = phoneNumber;
        IsActive = true;
    }

    /// <summary>
    /// Nombre que se mostrará en el sistema.
    /// No necesariamente corresponde al nombre legal.
    /// </summary>
    public string DisplayName { get; protected set; } = string.Empty;

    /// <summary>
    /// Identificación tributaria o documento oficial.
    /// </summary>
    public TaxIdentifier TaxIdentifier { get; protected set; } = default!;

    /// <summary>
    /// Correo electrónico principal.
    /// </summary>
    public Email? Email { get; protected set; }

    /// <summary>
    /// Teléfono principal.
    /// </summary>
    public PhoneNumber? PhoneNumber { get; protected set; }

    /// <summary>
    /// Indica si el participante puede intervenir en nuevos expedientes.
    /// </summary>
    public bool IsActive { get; protected set; }

    /// <summary>
    /// Actualiza el nombre visible.
    /// </summary>
    public void UpdateDisplayName(string displayName)
    {
        DisplayName = displayName.Trim();
    }

    /// <summary>
    /// Actualiza el correo electrónico.
    /// </summary>
    public void UpdateEmail(Email? email)
    {
        Email = email;
    }

    /// <summary>
    /// Actualiza el teléfono principal.
    /// </summary>
    public void UpdatePhoneNumber(PhoneNumber? phoneNumber)
    {
        PhoneNumber = phoneNumber;
    }

    /// <summary>
    /// Activa el participante.
    /// </summary>
    public void Activate()
    {
        IsActive = true;
    }

    /// <summary>
    /// Desactiva el participante.
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
    }
}