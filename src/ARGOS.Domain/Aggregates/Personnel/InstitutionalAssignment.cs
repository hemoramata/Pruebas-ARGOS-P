namespace ARGOS.Domain.Aggregates.Personnel;

public sealed class InstitutionalAssignment
{
    internal InstitutionalAssignment(Guid personId, Guid positionId, DateTimeOffset startsAt,
        DateTimeOffset? endsAt, string authorizationReference)
    {
        if (endsAt.HasValue && endsAt.Value <= startsAt)
            throw new ArgumentException("Assignment end must follow its start.");
        AuthorizationReference = OfficialIdentity.Required(authorizationReference, 200);
        PersonId = personId;
        PositionId = positionId;
        StartsAt = startsAt.ToUniversalTime();
        EndsAt = endsAt?.ToUniversalTime();
    }

    public Guid Id { get; } = Guid.NewGuid();
    public Guid PersonId { get; }
    public Guid PositionId { get; }
    public DateTimeOffset StartsAt { get; }
    public DateTimeOffset? EndsAt { get; }
    public string AuthorizationReference { get; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public string? RevocationAuthorizationReference { get; private set; }

    public bool IsEffectiveAt(DateTimeOffset instant) => instant >= StartsAt
        && (!EndsAt.HasValue || instant < EndsAt.Value)
        && (!RevokedAt.HasValue || instant < RevokedAt.Value);

    internal bool Overlaps(DateTimeOffset start, DateTimeOffset? end)
    {
        var effectiveEnd = EndsAt;
        if (RevokedAt.HasValue && (!effectiveEnd.HasValue || RevokedAt < effectiveEnd))
            effectiveEnd = RevokedAt;
        return (!effectiveEnd.HasValue || start < effectiveEnd.Value)
            && (!end.HasValue || StartsAt < end.Value)
            && (!effectiveEnd.HasValue || effectiveEnd.Value > StartsAt);
    }

    internal void Revoke(DateTimeOffset instant, string authorizationReference)
    {
        if (RevokedAt.HasValue) throw new InvalidOperationException("Assignment already revoked.");
        RevocationAuthorizationReference = OfficialIdentity.Required(authorizationReference, 200);
        RevokedAt = instant.ToUniversalTime();
    }
}
