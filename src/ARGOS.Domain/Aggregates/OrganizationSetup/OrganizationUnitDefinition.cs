namespace ARGOS.Domain.Aggregates.OrganizationSetup;

public sealed class OrganizationUnitDefinition
{
    private OrganizationUnitDefinition()
    {
    }

    internal OrganizationUnitDefinition(
        Guid id,
        string code,
        string name,
        Guid? parentUnitId)
    {
        Id = id;
        Code = code;
        Name = name;
        ParentUnitId = parentUnitId;
    }

    public Guid Id { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public Guid? ParentUnitId { get; private set; }

    internal void Rename(string name) => Name = name;

    internal OrganizationUnitDefinition Clone()
        => new(Id, Code, Name, ParentUnitId);
}
