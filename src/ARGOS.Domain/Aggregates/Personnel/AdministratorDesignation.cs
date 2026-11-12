namespace ARGOS.Domain.Aggregates.Personnel;

/// <summary>Institutional appointment tied to a specific assignment, never to installation.</summary>
public sealed class AdministratorDesignation
{
    private AdministratorDesignation() { }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid OrganizationId { get; private set; }
    public Guid PersonId { get; private set; }
    public Guid AssignmentId { get; private set; }
    public string AuthorizationReference { get; private set; } = "";
    public string AuthorizedBy { get; private set; } = "";
    public string RecordedBy { get; private set; } = "";
    public DateTimeOffset RecordedAt { get; private set; }
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset? EndsAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public string? RevocationReference { get; private set; }

    public static AdministratorDesignation Create(InstitutionalPerson person, Guid assignmentId,
        string authorizationReference, string authorizedBy, string recordedBy,
        DateTimeOffset recordedAt, DateTimeOffset startsAt, DateTimeOffset? endsAt)
    {
        ArgumentNullException.ThrowIfNull(person);
        var assignment = person.Assignments.SingleOrDefault(a => a.Id == assignmentId)
            ?? throw new ArgumentException("Assignment must belong to the designated person.");
        if (endsAt.HasValue && endsAt <= startsAt)
            throw new ArgumentException("Designation end must follow its start.");
        if (startsAt < assignment.StartsAt || assignment.RevokedAt.HasValue
            || (assignment.EndsAt.HasValue && (!endsAt.HasValue || endsAt > assignment.EndsAt)))
            throw new ArgumentException("Designation must remain within the assignment period.");
        return new AdministratorDesignation
        {
            OrganizationId = person.OrganizationId.Value, PersonId = person.Id, AssignmentId = assignmentId,
            AuthorizationReference = OfficialIdentity.Required(authorizationReference, 200),
            AuthorizedBy = OfficialIdentity.Required(authorizedBy, 200),
            RecordedBy = OfficialIdentity.Required(recordedBy, 200), RecordedAt = recordedAt.ToUniversalTime(),
            StartsAt = startsAt.ToUniversalTime(), EndsAt = endsAt?.ToUniversalTime()
        };
    }

    public bool IsEffectiveAt(DateTimeOffset instant) => instant >= StartsAt
        && (!EndsAt.HasValue || instant < EndsAt) && (!RevokedAt.HasValue || instant < RevokedAt);

    public void Revoke(DateTimeOffset instant, string authorizationReference)
    {
        if (RevokedAt.HasValue) throw new InvalidOperationException("Designation already revoked.");
        if (instant < RecordedAt) throw new ArgumentException("Revocation cannot precede registration.");
        RevocationReference = OfficialIdentity.Required(authorizationReference, 200);
        RevokedAt = instant.ToUniversalTime();
    }
}
