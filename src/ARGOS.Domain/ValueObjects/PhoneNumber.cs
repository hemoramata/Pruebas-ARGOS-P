using System.Text.RegularExpressions;
using ARGOS.SharedKernel.Abstractions;
using ARGOS.SharedKernel.Results;

namespace ARGOS.Domain.ValueObjects;

/// <summary>
/// Representa un número telefónico válido dentro del dominio.
/// </summary>
public sealed class PhoneNumber : ValueObject
{
    /// <summary>
    /// Longitud mínima permitida.
    /// </summary>
    public const int MinLength = 8;

    /// <summary>
    /// Longitud máxima permitida según la recomendación E.164.
    /// </summary>
    public const int MaxLength = 15;

    /// <summary>
    /// Obtiene el número telefónico.
    /// </summary>
    public string Value { get; }

    private PhoneNumber(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Crea una instancia válida de <see cref="PhoneNumber"/>.
    /// </summary>
    /// <param name="value">Número telefónico.</param>
    /// <returns>Resultado con el Value Object.</returns>
    public static Result<PhoneNumber> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result<PhoneNumber>.Failure(
                new Error(
                    "PhoneNumber.Required",
                    "El número telefónico es obligatorio."));
        }

        value = value.Trim();

        // Elimina espacios, guiones y paréntesis.
        value = value
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty)
            .Replace("(", string.Empty)
            .Replace(")", string.Empty);

        if (value.StartsWith('+'))
        {
            value = "+" + value[1..].Replace("+", string.Empty);
        }

        var digits = value.StartsWith('+')
            ? value[1..]
            : value;

        if (!Regex.IsMatch(digits, @"^\d+$"))
        {
            return Result<PhoneNumber>.Failure(
                new Error(
                    "PhoneNumber.Invalid",
                    "El número telefónico contiene caracteres inválidos."));
        }

        if (digits.Length < MinLength || digits.Length > MaxLength)
        {
            return Result<PhoneNumber>.Failure(
                new Error(
                    "PhoneNumber.Length",
                    $"El número telefónico debe tener entre {MinLength} y {MaxLength} dígitos."));
        }

        return Result<PhoneNumber>.Success(new PhoneNumber(value));
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>
    /// Conversión implícita a string.
    /// </summary>
    public static implicit operator string(PhoneNumber phoneNumber) => phoneNumber.Value;
}