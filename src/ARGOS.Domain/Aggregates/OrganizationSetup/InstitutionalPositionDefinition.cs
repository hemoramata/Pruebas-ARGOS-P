namespace ARGOS.Domain.Aggregates.OrganizationSetup;

public sealed class InstitutionalPositionDefinition
{
    private List<Guid> _competenceIds = [];
    private List<Guid> _actingCapacityIds = [];

    private InstitutionalPositionDefinition()
    {
    }

    internal InstitutionalPositionDefinition(
        Guid id,
        string code,
        string name,
        Guid organizationUnitId,
        IEnumerable<Guid>? competenceIds = null,
        IEnumerable<Guid>? actingCapacityIds = null)
    {
        Id = id;
        Code = code;
        Name = name;
        OrganizationUnitId = organizationUnitId;
        _competenceIds = competenceIds?.ToList() ?? [];
        _actingCapacityIds = actingCapacityIds?.ToList() ?? [];
    }

    public Guid Id { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public Guid OrganizationUnitId { get; private set; }

    public IReadOnlyCollection<Guid> CompetenceIds => _competenceIds.AsReadOnly();

    public IReadOnlyCollection<Guid> ActingCapacityIds => _actingCapacityIds.AsReadOnly();

    internal void Rename(string name) => Name = name;

    internal void AddCompetence(Guid competenceId)
    {
        if (!_competenceIds.Contains(competenceId))
            _competenceIds.Add(competenceId);
    }

    internal void AddActingCapacity(Guid actingCapacityId)
    {
        if (!_actingCapacityIds.Contains(actingCapacityId))
            _actingCapacityIds.Add(actingCapacityId);
    }

    internal InstitutionalPositionDefinition Clone()
        => new(
            Id,
            Code,
            Name,
            OrganizationUnitId,
            _competenceIds,
            _actingCapacityIds);
}
