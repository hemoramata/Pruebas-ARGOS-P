using ARGOS.Domain.ValueObjects;

namespace ARGOS.Domain.Entities;

/// <summary>
/// Representa una organización que puede participar
/// en uno o varios expedientes.
/// </summary>
public class Organization : Party
{
    protected Organization()
    {
    }

    public Organization(
        Guid id,
        string legalName,
        TaxIdentifier taxIdentifier,
        Email? email,
        PhoneNumber? phoneNumber)
        : base(
            id,
            legalName,
            taxIdentifier,
            email,
            phoneNumber)
    {
        LegalName = legalName.Trim();
    }

    /// <summary>
    /// Nombre legal o razón social de la organización.
    /// </summary>
    public string LegalName { get; private set; } = string.Empty;

    /// <summary>
    /// Actualiza la razón social.
    /// </summary>
    public void UpdateLegalName(string legalName)
    {
        LegalName = legalName.Trim();

        UpdateDisplayName(LegalName);
    }
}