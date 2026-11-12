namespace ARGOS.Domain.Aggregates.Personnel;

/// <summary>Identity is separate from authentication. No country-specific format is assumed.</summary>
public sealed record OfficialIdentity
{
    public string Issuer { get; }
    public string DocumentType { get; }
    public string Number { get; }

    public OfficialIdentity(string issuer, string documentType, string number)
    {
        Issuer = Required(issuer, 100);
        DocumentType = Required(documentType, 100);
        Number = Required(number, 100);
    }

    internal static string Required(string value, int limit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        if (normalized.Length > limit || normalized.Any(char.IsControl))
            throw new ArgumentException("Invalid text length or control characters.", nameof(value));
        return normalized;
    }

    // Avoid accidentally disclosing a document number through ordinary diagnostics.
    public override string ToString() => "OfficialIdentity [protected]";
}
