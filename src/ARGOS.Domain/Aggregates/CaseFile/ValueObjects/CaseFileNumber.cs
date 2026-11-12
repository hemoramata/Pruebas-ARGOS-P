using System.Text.RegularExpressions;
using ARGOS.SharedKernel.Results;

namespace ARGOS.Domain.Aggregates.CaseFile.ValueObjects;

/// <summary>
/// Identificador institucional del expediente.
/// Su formato será definido por las reglas de negocio.
/// </summary>
public sealed partial record CaseFileNumber
{
    private static readonly Error InvalidFormat = new(
        "CaseFile.InvalidNumberFormat",
        "The case file number must use TT-DD-CCCC-AAAA, for example US-DI-0001-2026.");

    public string Value { get; }

    private CaseFileNumber(string value)
    {
        Value = value;
    }

    public static Result<CaseFileNumber> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result<CaseFileNumber>.Failure(InvalidFormat);

        var normalized = value.Trim().ToUpperInvariant();
        var match = OfficialFormat().Match(normalized);

        if (!match.Success
            || match.Groups["sequence"].Value == "0000")
        {
            return Result<CaseFileNumber>.Failure(InvalidFormat);
        }

        return Result<CaseFileNumber>.Success(new CaseFileNumber(normalized));
    }

    public override string ToString() => Value;

    [GeneratedRegex(
        "^(?<type>[A-Z0-9]{2})-(?<department>[A-Z0-9]{2})-(?<sequence>[0-9]{4})-(?<year>[0-9]{4})$",
        RegexOptions.CultureInvariant)]
    private static partial Regex OfficialFormat();
}
