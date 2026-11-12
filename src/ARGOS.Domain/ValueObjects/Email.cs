using System.Net.Mail;
using ARGOS.SharedKernel.Abstractions;
using ARGOS.SharedKernel.Results;

namespace ARGOS.Domain.ValueObjects;

/// <summary>
/// Representa una dirección de correo electrónico válida dentro del dominio.
/// </summary>
public sealed class Email : ValueObject
{
    /// <summary>
    /// Obtiene el valor del correo electrónico.
    /// </summary>
    public string Value { get; }

    private Email(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Crea una instancia válida de <see cref="Email"/>.
    /// </summary>
    /// <param name="value">Correo electrónico.</param>
    /// <returns>Resultado con el objeto Email.</returns>
    public static Result<Email> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result<Email>.Failure(
                new Error(
                    "Email.Required",
                    "El correo electrónico es obligatorio."));
        }

        value = value.Trim().ToLowerInvariant();

        if (value.Length > 254)
        {
            return Result<Email>.Failure(
                new Error(
                    "Email.Length",
                    "El correo electrónico supera la longitud permitida."));
        }

        try
        {
            var address = new MailAddress(value);

            if (!string.Equals(address.Address, value, StringComparison.OrdinalIgnoreCase))
            {
                return Result<Email>.Failure(
                    new Error(
                        "Email.Invalid",
                        "El correo electrónico no tiene un formato válido."));
            }
        }
        catch
        {
            return Result<Email>.Failure(
                new Error(
                    "Email.Invalid",
                    "El correo electrónico no tiene un formato válido."));
        }

        return Result<Email>.Success(new Email(value));
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    /// <summary>
    /// Devuelve la representación en texto del correo electrónico.
    /// </summary>
    public override string ToString() => Value;

    /// <summary>
    /// Conversión implícita a string.
    /// </summary>
    /// <param name="email">Correo electrónico.</param>
    public static implicit operator string(Email email) => email.Value;
}