using ARGOS.Domain.Aggregates.Organization;
using ARGOS.Domain.Aggregates.OrganizationSetup;

namespace ARGOS.Application.Modules.OrganizationSetup;

public sealed record OrganizationSetupDto(
    Guid Id,
    OrganizationDto Organization,
    int Version,
    Guid? PreviousVersionId,
    Guid? NextVersionId,
    string Status,
    string CreatedByReference,
    string CreationPolicyVersion,
    string CreationAuthorizationReference,
    DateTime CreatedAtUtc,
    string? PublishedByReference,
    string? PublicationAuthorizationReference,
    DateTime? PublishedAtUtc,
    IReadOnlyCollection<OrganizationTermDto> Terms,
    IReadOnlyCollection<OrganizationUnitDefinitionDto> Units,
    IReadOnlyCollection<InstitutionalPositionDefinitionDto> Positions,
    IReadOnlyCollection<CompetenceDefinitionDto> Competences,
    IReadOnlyCollection<ActingCapacityDefinitionDto> ActingCapacities)
{
    public static OrganizationSetupDto From(
        Organization organization,
        Domain.Aggregates.OrganizationSetup.OrganizationSetup setup)
        => new(
            setup.Id.Value,
            OrganizationDto.From(organization),
            setup.Version,
            setup.PreviousVersionId?.Value,
            setup.NextVersionId?.Value,
            setup.Status.ToString(),
            setup.CreatedByReference,
            setup.CreationPolicyVersion,
            setup.CreationAuthorizationReference,
            setup.CreatedAtUtc,
            setup.PublishedByReference,
            setup.PublicationAuthorizationReference,
            setup.PublishedAtUtc,
            setup.Terms.OrderBy(item => item.Concept).Select(OrganizationTermDto.From).ToArray(),
            setup.Units.OrderBy(item => item.Code).Select(OrganizationUnitDefinitionDto.From).ToArray(),
            setup.Positions.OrderBy(item => item.Code).Select(InstitutionalPositionDefinitionDto.From).ToArray(),
            setup.Competences.OrderBy(item => item.Code).Select(CompetenceDefinitionDto.From).ToArray(),
            setup.ActingCapacities.OrderBy(item => item.Code).Select(ActingCapacityDefinitionDto.From).ToArray());
}

public sealed record OrganizationDto(Guid Id, string Name, string Code, string? Description, string Status)
{
    public static OrganizationDto From(Organization organization)
        => new(
            organization.Id.Value,
            organization.Name,
            organization.Code,
            organization.Description,
            organization.Status.ToString());
}

public sealed record OrganizationTermDto(string Concept, string Singular, string Plural)
{
    public static OrganizationTermDto From(OrganizationTerm term)
        => new(term.Concept.ToString(), term.Singular, term.Plural);
}

public sealed record OrganizationUnitDefinitionDto(Guid Id, string Code, string Name, Guid? ParentUnitId)
{
    public static OrganizationUnitDefinitionDto From(OrganizationUnitDefinition unit)
        => new(unit.Id, unit.Code, unit.Name, unit.ParentUnitId);
}

public sealed record InstitutionalPositionDefinitionDto(
    Guid Id,
    string Code,
    string Name,
    Guid OrganizationUnitId,
    IReadOnlyCollection<Guid> CompetenceIds,
    IReadOnlyCollection<Guid> ActingCapacityIds)
{
    public static InstitutionalPositionDefinitionDto From(InstitutionalPositionDefinition position)
        => new(
            position.Id,
            position.Code,
            position.Name,
            position.OrganizationUnitId,
            position.CompetenceIds.ToArray(),
            position.ActingCapacityIds.ToArray());
}

public sealed record CompetenceDefinitionDto(Guid Id, string Code, string Name, string? Description)
{
    public static CompetenceDefinitionDto From(CompetenceDefinition competence)
        => new(competence.Id, competence.Code, competence.Name, competence.Description);
}

public sealed record ActingCapacityDefinitionDto(Guid Id, string Code, string Name, string? Description)
{
    public static ActingCapacityDefinitionDto From(ActingCapacityDefinition capacity)
        => new(capacity.Id, capacity.Code, capacity.Name, capacity.Description);
}
