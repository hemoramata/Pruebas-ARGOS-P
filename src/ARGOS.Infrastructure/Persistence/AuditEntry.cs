namespace ARGOS.Infrastructure.Persistence;

/// <summary>Registro inmutable de una acción ejecutada dentro de ARGOS.</summary>
public sealed class AuditEntry
{
    private AuditEntry() { }

    public Guid Id { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public string ActorReference { get; private set; } = string.Empty;
    public string? SessionReference { get; private set; }
    public string ActionCode { get; private set; } = string.Empty;
    public string SubjectType { get; private set; } = string.Empty;
    public string SubjectReference { get; private set; } = string.Empty;
    public string Outcome { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public string RequestReference { get; private set; } = string.Empty;
    public string? Details { get; private set; }

    public static AuditEntry Create(
        Guid? organizationId,
        string actorReference,
        string? sessionReference,
        string actionCode,
        string subjectType,
        string subjectReference,
        string outcome,
        string requestReference,
        string? details = null)
        => new()
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            ActorReference = Required(actorReference, 200),
            SessionReference = Optional(sessionReference, 200),
            ActionCode = Required(actionCode, 120),
            SubjectType = Required(subjectType, 120),
            SubjectReference = Required(subjectReference, 240),
            Outcome = Required(outcome, 40),
            OccurredAtUtc = DateTimeOffset.UtcNow,
            RequestReference = Required(requestReference, 200),
            Details = Optional(details, 4000)
        };

    private static string Required(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Audit value is required.");
        var normalized = value.Trim();
        if (normalized.Length > max) throw new ArgumentException("Audit value is too long.");
        return normalized;
    }

    private static string? Optional(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        if (normalized.Length > max) throw new ArgumentException("Audit value is too long.");
        return normalized;
    }
}
