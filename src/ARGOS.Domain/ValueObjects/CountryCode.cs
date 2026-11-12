using ARGOS.SharedKernel.Abstractions;
using ARGOS.SharedKernel.Results;
using System.Collections.Generic;

namespace ARGOS.Domain.ValueObjects;

/// <summary>
/// Representa un código de país válido para ARGOS ERP.
/// En la versión 1.0 se valida contra el catálogo institucional.
/// La versión 2.0 permitirá seleccionar el estándar de validación.
/// </summary>
public sealed class CountryCode : ValueObject
{
    private static readonly HashSet<string> ValidCodes = new()
    {
        "CRI",
        "USA",
        "MEX",
        "PAN",
        "NIC",
        "HND",
        "SLV",
        "GTM",
        "BLZ",
        "CAN",
        "BRA",
        "ARG",
        "COL",
        "PER",
        "CHL",
        "ECU",
        "URY",
        "PRY",
        "BOL",
        "ESP",
        "FRA",
        "DEU",
        "ITA",
        "GBR",
        "PRT",
        "JPN",
        "CHN",
        "KOR",
        "IND",
        "AUS",
        "NZL"
    };

    public string Value { get; }

    private CountryCode(string value)
    {
        Value = value;
    }

    public static Result<CountryCode> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result<CountryCode>.Failure(
                new Error(
                    "CountryCode.Required",
                    "El código del país es obligatorio."));
        }

        value = value.Trim().ToUpperInvariant();

        if (!ValidCodes.Contains(value))
        {
            return Result<CountryCode>.Failure(
                new Error(
                    "CountryCode.Invalid",
                    "El código del país no es válido."));
        }

        return Result<CountryCode>.Success(new CountryCode(value));
    }

    public static implicit operator string(CountryCode countryCode)
        => countryCode.Value;

    public override string ToString()
        => Value;

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}