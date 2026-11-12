using System.Text.RegularExpressions;
using ARGOS.Domain.Enums;
using ARGOS.SharedKernel.Abstractions;
using ARGOS.SharedKernel.Results;

namespace ARGOS.Domain.ValueObjects;

/// <summary>
/// Representa un identificador oficial costarricense.
/// </summary>
public sealed class TaxIdentifier : ValueObject
{
    private TaxIdentifier(string value, TaxIdentifierType type)
    {
        Value = value;
        Type = type;
    }

    public string Value { get; }

    public TaxIdentifierType Type { get; }

    public bool IsNaturalPerson => Type == TaxIdentifierType.NaturalPerson;
    public bool IsLegalEntity => Type == TaxIdentifierType.LegalEntity;
    public bool IsDimex => Type == TaxIdentifierType.DIMEX;
    public bool IsNite => Type == TaxIdentifierType.NITE;

    public string FormattedValue => Type switch
    {
        TaxIdentifierType.NaturalPerson when Value.Length == 9
            => $"{Value[0]}-{Value.Substring(1, 4)}-{Value.Substring(5, 4)}",

        TaxIdentifierType.LegalEntity when Value.Length == 10
            => $"{Value[0]}-{Value.Substring(1, 3)}-{Value.Substring(4, 6)}",

        _ => Value
    };

    public static Result<TaxIdentifier> Create(string value, TaxIdentifierType type)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result<TaxIdentifier>.Failure(
                new Error("TaxIdentifier.Empty", "La identificación es requerida."));

        var normalized = Regex.Replace(value, @"[^\d]", "");

        if (!Validate(normalized, type))
        {
            return Result<TaxIdentifier>.Failure(
                new Error("TaxIdentifier.Invalid",
                    $"La identificación no corresponde al tipo {type}."));
        }

        return Result<TaxIdentifier>.Success(
            new TaxIdentifier(normalized, type));
    }

    public static Result<TaxIdentifier> CreateAuto(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result<TaxIdentifier>.Failure(
                new Error("TaxIdentifier.Empty", "La identificación es requerida."));

        var normalized = Regex.Replace(value, @"[^\d]", "");

        TaxIdentifierType? detected = normalized.Length switch
        {
            9 => TaxIdentifierType.NaturalPerson,
            10 when normalized.StartsWith("3") => TaxIdentifierType.LegalEntity,
            10 => TaxIdentifierType.NITE,
            11 or 12 => TaxIdentifierType.DIMEX,
            _ => null
        };

        if (detected is null)
            return Result<TaxIdentifier>.Failure(
                new Error("TaxIdentifier.UnknownType",
                    "No fue posible determinar el tipo de identificación."));

        return Create(normalized, detected.Value);
    }

    private static bool Validate(string value, TaxIdentifierType type)
    {
        if (!Regex.IsMatch(value, @"^\d+$"))
            return false;

        return type switch
        {
            TaxIdentifierType.NaturalPerson => value.Length == 9,
            TaxIdentifierType.LegalEntity => value.Length == 10 && value.StartsWith("3"),
            TaxIdentifierType.DIMEX => value.Length is 11 or 12,
            TaxIdentifierType.NITE => value.Length == 10,
            _ => false
        };
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
        yield return Type;
    }

    public static implicit operator string(TaxIdentifier identifier)
        => identifier.Value;

    public override string ToString() => FormattedValue;
}
