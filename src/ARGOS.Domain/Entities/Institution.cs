using ARGOS.Domain.ValueObjects;

namespace ARGOS.Domain.Entities;

/// <summary>
/// Representa una institución pública que participa
/// en la gestión de expedientes y trámites.
/// </summary>
public class Institution : Organization
{
    protected Institution()
    {
    }

    public Institution(
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
    }
}