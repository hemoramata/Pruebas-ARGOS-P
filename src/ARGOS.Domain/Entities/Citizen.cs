using ARGOS.Domain.ValueObjects;

namespace ARGOS.Domain.Entities;

/// <summary>
/// Representa una persona física que puede participar
/// en uno o varios expedientes.
/// </summary>
public sealed class Citizen : Party
{
    private Citizen()
    {
    }

    public Citizen(
        Guid id,
        string firstName,
        string? middleName,
        string lastName,
        string? secondLastName,
        DateOnly? birthDate,
        TaxIdentifier taxIdentifier,
        Email? email,
        PhoneNumber? phoneNumber)
        : base(
            id,
            BuildDisplayName(
                firstName,
                middleName,
                lastName,
                secondLastName),
            taxIdentifier,
            email,
            phoneNumber)
    {
        FirstName = firstName.Trim();
        MiddleName = middleName?.Trim();
        LastName = lastName.Trim();
        SecondLastName = secondLastName?.Trim();
        BirthDate = birthDate;
    }

    /// <summary>
    /// Primer nombre.
    /// </summary>
    public string FirstName { get; private set; } = string.Empty;

    /// <summary>
    /// Segundo nombre.
    /// </summary>
    public string? MiddleName { get; private set; }

    /// <summary>
    /// Primer apellido.
    /// </summary>
    public string LastName { get; private set; } = string.Empty;

    /// <summary>
    /// Segundo apellido.
    /// </summary>
    public string? SecondLastName { get; private set; }

    /// <summary>
    /// Fecha de nacimiento.
    /// </summary>
    public DateOnly? BirthDate { get; private set; }

    /// <summary>
    /// Actualiza el nombre completo del ciudadano.
    /// </summary>
    public void UpdateName(
        string firstName,
        string? middleName,
        string lastName,
        string? secondLastName)
    {
        FirstName = firstName.Trim();
        MiddleName = middleName?.Trim();
        LastName = lastName.Trim();
        SecondLastName = secondLastName?.Trim();

        UpdateDisplayName(
            BuildDisplayName(
                FirstName,
                MiddleName,
                LastName,
                SecondLastName));
    }

    /// <summary>
    /// Actualiza la fecha de nacimiento.
    /// </summary>
    public void UpdateBirthDate(DateOnly? birthDate)
    {
        BirthDate = birthDate;
    }

    private static string BuildDisplayName(
        string firstName,
        string? middleName,
        string lastName,
        string? secondLastName)
    {
        return string.Join(
            " ",
            new[]
            {
                firstName.Trim(),
                middleName?.Trim(),
                lastName.Trim(),
                secondLastName?.Trim()
            }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
    }
}