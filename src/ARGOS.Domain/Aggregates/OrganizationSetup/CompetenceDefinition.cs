namespace ARGOS.Domain.Aggregates.OrganizationSetup;

public sealed class CompetenceDefinition
{
    private CompetenceDefinition()
    {
    }

    internal CompetenceDefinition(
        Guid id,
        string code,
        string name,
        string? description)
    {
        Id = id;
        Code = code;
        Name = name;
        Description = description;
    }

    public Guid Id { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    internal CompetenceDefinition Clone() => new(Id, Code, Name, Description);
}
