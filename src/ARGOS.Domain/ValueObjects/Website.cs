using ARGOS.SharedKernel.Abstractions;
using ARGOS.SharedKernel.Results;

namespace ARGOS.Domain.ValueObjects;

/// <summary>
/// Representa un sitio web válido dentro del dominio.
/// </summary>
public sealed class Website : ValueObject
{
    /// <summary>
    /// Longitud máxima permitida para una URL.
    /// </summary>
    public const int MaxLength = 2048;

    /// <summary>
    /// Obtiene la dirección del sitio web.
    /// </summary>
    public string Value { get; }

    private Website(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Crea una instancia válida de <see cref="Website"/>.
    /// </summary>
    /// <param name="value">Dirección del sitio web.</param>
    /// <returns>Resultado con el Value Object.</returns>
    public static Result<Website> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result<Website>.Failure(
                new Error(
                    "Website.Required",
                    "El sitio web es obligatorio."));
        }

        value = value.Trim();

        if (value.Length > MaxLength)
        {
            return Result<Website>.Failure(
                new Error(
                    "Website.Length",
                    $"El sitio web no puede superar los {MaxLength} caracteres."));
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return Result<Website>.Failure(
                new Error(
                    "Website.Invalid",
                    "La dirección del sitio web no es válida."));
        }

        if (uri.Scheme != Uri.UriSchemeHttp &&
            uri.Scheme != Uri.UriSchemeHttps)
        {
            return Result<Website>.Failure(
                new Error(
                    "Website.Scheme",
                    "El sitio web debe utilizar HTTP o HTTPS."));
        }

        return Result<Website>.Success(new Website(uri.AbsoluteUri));
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    /// <summary>
    /// Devuelve la representación en texto del sitio web.
    /// </summary>
    public override string ToString() => Value;

    /// <summary>
    /// Conversión implícita a string.
    /// </summary>
    /// <param name="website">Sitio web.</param>
    public static implicit operator string(Website website) => website.Value;
}