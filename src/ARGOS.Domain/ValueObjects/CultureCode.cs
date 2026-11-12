using ARGOS.SharedKernel.Abstractions;
using ARGOS.SharedKernel.Results;
using System.Collections.Generic;
using System.Globalization;

namespace ARGOS.Domain.ValueObjects;

/// <summary>
/// Representa una configuración regional (Culture) válida.
/// Utiliza las culturas soportadas por .NET.
/// Ejemplos:
/// es-CR, en-US, es-MX, fr-FR.
/// </summary>
public sealed class CultureCode : ValueObject
{
    public string Value { get; }

    private CultureCode(string value)
    {
        Value = value;
    }

    public static Result<CultureCode> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result<CultureCode>.Failure(
                new Error(
                    "CultureCode.Required",
                    "La configuración regional es obligatoria."));
        }

        value = value.Trim();

        try
        {
            var culture = CultureInfo.GetCultureInfo(value);

            return Result<CultureCode>.Success(
                new CultureCode(culture.Name));
        }
        catch (CultureNotFoundException)
        {
            return Result<CultureCode>.Failure(
                new Error(
                    "CultureCode.Invalid",
                    "La configuración regional no es válida."));
        }
    }

    public static implicit operator string(CultureCode cultureCode)
        => cultureCode.Value;

    public override string ToString()
        => Value;

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}