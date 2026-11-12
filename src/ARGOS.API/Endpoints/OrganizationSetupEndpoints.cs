using ARGOS.Application.Modules.OrganizationSetup;
using ARGOS.Domain.Aggregates.OrganizationSetup.Enums;
using ARGOS.SharedKernel.Results;

namespace ARGOS.API.Endpoints;

public static class OrganizationSetupEndpoints
{
    public static IEndpointRouteBuilder MapOrganizationSetupEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/organization-setups")
            .WithTags("Organization setup");
        group.AddEndpointFilter<OrganizationSetupAccessFilter>();

        group.MapGet("/current", async (
            OrganizationSetupService service,
            CancellationToken cancellationToken) =>
        {
            var setup = await service.GetCurrentAsync(cancellationToken);
            return setup is null ? Results.NoContent() : Results.Ok(setup);
        })
        .WithName("GetCurrentOrganizationSetup");

        group.MapGet("/{id:guid}", async (
            Guid id,
            OrganizationSetupService service,
            CancellationToken cancellationToken) =>
        {
            var setup = await service.GetByIdAsync(id, cancellationToken);
            return setup is null
                ? Results.NotFound(OrganizationSetupApplicationErrors.NotFound)
                : Results.Ok(setup);
        })
        .WithName("GetOrganizationSetup");

        group.MapGet("/organizations/{organizationId:guid}/versions", async (
            Guid organizationId,
            OrganizationSetupService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.ListVersionsAsync(
                organizationId, cancellationToken)))
        .WithName("ListOrganizationSetupVersions");

        group.MapPost("/", async (
            CreateOrganizationSetupRequest request,
            OrganizationSetupService service,
            CancellationToken cancellationToken) =>
        {
            if (request.Action is null)
                return MissingAction();

            var result = await service.CreateInitialAsync(
                request.OrganizationName,
                request.OrganizationCode,
                request.Description,
                request.Action.ToInput(),
                cancellationToken);

            return result.IsSuccess
                ? Results.Created($"/api/organization-setups/{result.Value.Id}", result.Value)
                : ToHttpResult(result);
        })
        .WithName("CreateOrganizationSetup");

        group.MapPut("/{id:guid}/terms/{concept}", async (
            Guid id,
            string concept,
            SetOrganizationTermRequest request,
            OrganizationSetupService service,
            CancellationToken cancellationToken) =>
        {
            if (!Enum.TryParse<OrganizationConcept>(concept, true, out var parsedConcept))
                return InvalidRequest("El concepto organizacional indicado no existe.");

            return ToHttpResult(await service.SetTermAsync(
                id, parsedConcept, request.Singular, request.Plural, cancellationToken));
        })
        .WithName("SetOrganizationTerm");

        group.MapPost("/{id:guid}/units", async (
            Guid id,
            CreateOrganizationUnitRequest request,
            OrganizationSetupService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.AddUnitAsync(
                id, request.Code, request.Name, request.ParentUnitId, cancellationToken)))
        .WithName("AddOrganizationUnit");

        group.MapPut("/{id:guid}/units/{unitId:guid}", async (
            Guid id,
            Guid unitId,
            RenameCatalogItemRequest request,
            OrganizationSetupService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.RenameUnitAsync(
                id, unitId, request.Name, cancellationToken)))
        .WithName("RenameOrganizationUnit");

        group.MapDelete("/{id:guid}/units/{unitId:guid}", async (
            Guid id, Guid unitId, OrganizationSetupService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.RemoveUnitAsync(id, unitId, cancellationToken)))
        .WithName("RemoveOrganizationUnit");

        group.MapPost("/{id:guid}/competences", async (
            Guid id,
            CreateCatalogItemRequest request,
            OrganizationSetupService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.AddCompetenceAsync(
                id, request.Code, request.Name, request.Description, cancellationToken)))
        .WithName("AddOrganizationCompetence");

        group.MapPost("/{id:guid}/acting-capacities", async (
            Guid id,
            CreateCatalogItemRequest request,
            OrganizationSetupService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.AddActingCapacityAsync(
                id, request.Code, request.Name, request.Description, cancellationToken)))
        .WithName("AddOrganizationActingCapacity");

        group.MapPost("/{id:guid}/positions", async (
            Guid id,
            CreateInstitutionalPositionRequest request,
            OrganizationSetupService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.AddPositionAsync(
                id,
                request.Code,
                request.Name,
                request.OrganizationUnitId,
                cancellationToken)))
        .WithName("AddInstitutionalPosition");

        group.MapPut("/{id:guid}/positions/{positionId:guid}", async (
            Guid id,
            Guid positionId,
            RenameCatalogItemRequest request,
            OrganizationSetupService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.RenamePositionAsync(
                id, positionId, request.Name, cancellationToken)))
        .WithName("RenameInstitutionalPosition");

        group.MapPost("/{id:guid}/positions/{positionId:guid}/competences/{competenceId:guid}", async (
            Guid id,
            Guid positionId,
            Guid competenceId,
            OrganizationSetupService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.AssignCompetenceAsync(
                id, positionId, competenceId, cancellationToken)))
        .WithName("AssignPositionCompetence");

        group.MapPost("/{id:guid}/positions/{positionId:guid}/acting-capacities/{capacityId:guid}", async (
            Guid id,
            Guid positionId,
            Guid capacityId,
            OrganizationSetupService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.AssignActingCapacityAsync(
                id, positionId, capacityId, cancellationToken)))
        .WithName("AssignPositionActingCapacity");

        group.MapPost("/{id:guid}/publish", async (
            Guid id,
            OrganizationSetupActionRequest request,
            OrganizationSetupService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.PublishAsync(
                id, request.ToInput(), cancellationToken)))
        .WithName("PublishOrganizationSetup");

        group.MapPost("/{id:guid}/versions", async (
            Guid id,
            OrganizationSetupActionRequest request,
            OrganizationSetupService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateNextVersionAsync(
                id, request.ToInput(), cancellationToken);
            return result.IsSuccess
                ? Results.Created($"/api/organization-setups/{result.Value.Id}", result.Value)
                : ToHttpResult(result);
        })
        .WithName("CreateOrganizationSetupVersion");

        return endpoints;
    }

    private static IResult ToHttpResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
            return Results.Ok(result.Value);

        if (result.Error == OrganizationSetupApplicationErrors.NotFound)
            return Results.NotFound(result.Error);

        if (result.Error == OrganizationSetupApplicationErrors.InvalidOrganization
            || result.Error == OrganizationSetupApplicationErrors.InvalidAction
            || result.Error.Code is "OrganizationSetup.InvalidActionContext"
                or "OrganizationSetup.InvalidCatalogItem")
        {
            return InvalidRequest(result.Error.Description);
        }

        return Results.Conflict(result.Error);
    }

    private static IResult MissingAction()
        => InvalidRequest("Los datos de responsabilidad y autorización son obligatorios.");

    private static IResult InvalidRequest(string description)
        => Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["request"] = [description]
        });
}

public sealed record CreateOrganizationSetupRequest(
    string? OrganizationName,
    string? OrganizationCode,
    string? Description,
    OrganizationSetupActionRequest? Action);

public sealed record OrganizationSetupActionRequest(
    string? ActorReference,
    string? PolicyVersion,
    string? AuthorizationReference)
{
    public OrganizationSetupActionInput ToInput()
        => new(ActorReference, PolicyVersion, AuthorizationReference);
}

public sealed record SetOrganizationTermRequest(string? Singular, string? Plural);

public sealed record CreateOrganizationUnitRequest(
    string? Code,
    string? Name,
    Guid? ParentUnitId);

public sealed record CreateCatalogItemRequest(
    string? Code,
    string? Name,
    string? Description);

public sealed record CreateInstitutionalPositionRequest(
    string? Code,
    string? Name,
    Guid OrganizationUnitId);

public sealed record RenameCatalogItemRequest(string? Name);
