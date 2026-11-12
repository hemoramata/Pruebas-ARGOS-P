using ARGOS.Domain.Aggregates.Organization.ValueObjects;
using ARGOS.Domain.Aggregates.OrganizationSetup.Enums;
using Setup = ARGOS.Domain.Aggregates.OrganizationSetup.OrganizationSetup;

namespace ARGOS.Domain.Aggregates.Personnel;

/// <summary>
/// Institutional identity and employment links, without credentials or session tokens.
/// Callers must supply the current published setup and trusted authorization context.
/// </summary>
public sealed class InstitutionalPerson
{
    private readonly List<InstitutionalAssignment> assignments = [];

    private InstitutionalPerson() { }

    public InstitutionalPerson(OrganizationId organizationId, OfficialIdentity identity,
        string fullName)
    {
        ArgumentNullException.ThrowIfNull(organizationId);
        ArgumentNullException.ThrowIfNull(identity);
        if (organizationId.Value == Guid.Empty) throw new ArgumentException("Organization is required.");
        OrganizationId = organizationId;
        Identity = identity;
        FullName = OfficialIdentity.Required(fullName, 200);
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public OrganizationId OrganizationId { get; private set; } = null!;
    public OfficialIdentity Identity { get; private set; } = null!;
    public string FullName { get; private set; } = string.Empty;
    public IReadOnlyList<InstitutionalAssignment> Assignments => assignments.AsReadOnly();

    public InstitutionalAssignment Assign(Setup currentSetup, Guid positionId,
        DateTimeOffset startsAt, DateTimeOffset? endsAt, string authorizationReference)
    {
        ValidateSetup(currentSetup);
        if (!currentSetup.Positions.Any(p => p.Id == positionId))
            throw new ArgumentException("Position does not belong to this published setup.");
        var assignment = new InstitutionalAssignment(Id, positionId, startsAt, endsAt, authorizationReference);
        if (assignments.Any(a => a.PositionId == positionId && a.Overlaps(startsAt, endsAt)))
            throw new InvalidOperationException("Overlapping assignment to the same position.");
        assignments.Add(assignment);
        return assignment;
    }

    public void RevokeAssignment(Guid assignmentId, DateTimeOffset instant, string authorizationReference)
    {
        var assignment = assignments.SingleOrDefault(a => a.Id == assignmentId)
            ?? throw new ArgumentException("Assignment does not belong to this person.");
        assignment.Revoke(instant, authorizationReference);
    }

    /// <summary>Competences are not API permissions; authorization policy must map actions separately.</summary>
    public IReadOnlyCollection<Guid> GetEffectiveCompetenceIds(Setup currentSetup, DateTimeOffset instant)
    {
        ValidateSetup(currentSetup);
        var positionIds = assignments.Where(a => a.IsEffectiveAt(instant)).Select(a => a.PositionId).ToHashSet();
        return currentSetup.Positions.Where(p => positionIds.Contains(p.Id))
            .SelectMany(p => p.CompetenceIds).Distinct().ToArray();
    }

    private void ValidateSetup(Setup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        if (!setup.OrganizationId.Equals(OrganizationId) || setup.Status != OrganizationSetupStatus.Published)
            throw new InvalidOperationException("A published setup from this organization is required.");
    }
}
