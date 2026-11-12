using ARGOS.Application.Common.Persistence;
using ARGOS.Domain.Aggregates.Organization;
using ARGOS.Domain.Aggregates.Organization.ValueObjects;
using ARGOS.Domain.Aggregates.OrganizationSetup.Enums;
using ARGOS.Domain.Aggregates.OrganizationSetup.ValueObjects;
using ARGOS.SharedKernel.Results;
using OrganizationSetupAggregate = ARGOS.Domain.Aggregates.OrganizationSetup.OrganizationSetup;

namespace ARGOS.Application.Modules.OrganizationSetup;

public sealed class OrganizationSetupService
{
    private readonly IOrganizationSetupRepository _repository;

    public OrganizationSetupService(IOrganizationSetupRepository repository)
    {
        _repository = repository;
    }

    public async Task<OrganizationSetupDto?> GetCurrentAsync(
        CancellationToken cancellationToken = default)
    {
        var setup = await _repository.GetCurrentAsync(cancellationToken);
        return setup is null ? null : await MapAsync(setup, cancellationToken);
    }

    public async Task<OrganizationSetupDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var setup = await _repository.GetByIdAsync(
            OrganizationSetupId.From(id), cancellationToken);
        return setup is null ? null : await MapAsync(setup, cancellationToken);
    }

    public async Task<Result<IReadOnlyCollection<OrganizationSetupDto>>> ListVersionsAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        var id = OrganizationId.From(organizationId);
        var organization = await _repository.GetOrganizationAsync(id, cancellationToken);
        if (organization is null)
        {
            return Result<IReadOnlyCollection<OrganizationSetupDto>>.Failure(
                OrganizationSetupApplicationErrors.NotFound);
        }

        var versions = await _repository.ListVersionsAsync(id, cancellationToken);
        IReadOnlyCollection<OrganizationSetupDto> result = versions
            .Select(setup => OrganizationSetupDto.From(organization, setup))
            .ToArray();
        return Result<IReadOnlyCollection<OrganizationSetupDto>>.Success(result);
    }

    public async Task<Result<OrganizationSetupDto>> CreateInitialAsync(
        string? organizationName,
        string? organizationCode,
        string? description,
        OrganizationSetupActionInput action,
        CancellationToken cancellationToken = default)
    {
        if (!IsValid(organizationName, 200)
            || !IsValid(organizationCode, 50)
            || (description?.Length ?? 0) > 2000)
        {
            return Result<OrganizationSetupDto>.Failure(
                OrganizationSetupApplicationErrors.InvalidOrganization);
        }

        var contextResult = CreateActionContext(action);
        if (contextResult.IsFailure)
            return Result<OrganizationSetupDto>.Failure(contextResult.Error);

        var organization = Organization.Create(
            organizationName!, organizationCode!, description);
        var setupResult = OrganizationSetupAggregate.CreateInitial(
            organization.Id, contextResult.Value);

        if (setupResult.IsFailure)
            return Result<OrganizationSetupDto>.Failure(setupResult.Error);

        var added = await _repository.TryAddInitialAsync(
            organization, setupResult.Value, cancellationToken);

        return added
            ? Result<OrganizationSetupDto>.Success(
                OrganizationSetupDto.From(organization, setupResult.Value))
            : Result<OrganizationSetupDto>.Failure(
                OrganizationSetupApplicationErrors.InstallationAlreadyExists);
    }

    public Task<Result<OrganizationSetupDto>> SetTermAsync(
        Guid id,
        OrganizationConcept concept,
        string? singular,
        string? plural,
        CancellationToken cancellationToken = default)
        => UpdateAsync(
            id,
            setup => setup.SetTerm(concept, singular, plural),
            cancellationToken);

    public Task<Result<OrganizationSetupDto>> AddUnitAsync(
        Guid id,
        string? code,
        string? name,
        Guid? parentUnitId,
        CancellationToken cancellationToken = default)
        => UpdateAsync(
            id,
            setup => setup.AddUnit(code, name, parentUnitId),
            cancellationToken);

    public Task<Result<OrganizationSetupDto>> RenameUnitAsync(
        Guid id,
        Guid unitId,
        string? name,
        CancellationToken cancellationToken = default)
        => UpdateAsync(id, setup => setup.RenameUnit(unitId, name), cancellationToken);

    public Task<Result<OrganizationSetupDto>> RemoveUnitAsync(Guid id, Guid unitId,
        CancellationToken cancellationToken = default)
        => UpdateAsync(id, setup => setup.RemoveUnit(unitId), cancellationToken);

    public Task<Result<OrganizationSetupDto>> AddCompetenceAsync(
        Guid id,
        string? code,
        string? name,
        string? description,
        CancellationToken cancellationToken = default)
        => UpdateAsync(
            id,
            setup => setup.AddCompetence(code, name, description),
            cancellationToken);

    public Task<Result<OrganizationSetupDto>> AddActingCapacityAsync(
        Guid id,
        string? code,
        string? name,
        string? description,
        CancellationToken cancellationToken = default)
        => UpdateAsync(
            id,
            setup => setup.AddActingCapacity(code, name, description),
            cancellationToken);

    public Task<Result<OrganizationSetupDto>> AddPositionAsync(
        Guid id,
        string? code,
        string? name,
        Guid organizationUnitId,
        CancellationToken cancellationToken = default)
        => UpdateAsync(
            id,
            setup => setup.AddPosition(code, name, organizationUnitId),
            cancellationToken);

    public Task<Result<OrganizationSetupDto>> RenamePositionAsync(
        Guid id,
        Guid positionId,
        string? name,
        CancellationToken cancellationToken = default)
        => UpdateAsync(id, setup => setup.RenamePosition(positionId, name), cancellationToken);

    public Task<Result<OrganizationSetupDto>> AssignCompetenceAsync(
        Guid id,
        Guid positionId,
        Guid competenceId,
        CancellationToken cancellationToken = default)
        => UpdateAsync(
            id,
            setup => setup.AssignCompetence(positionId, competenceId),
            cancellationToken);

    public Task<Result<OrganizationSetupDto>> AssignActingCapacityAsync(
        Guid id,
        Guid positionId,
        Guid actingCapacityId,
        CancellationToken cancellationToken = default)
        => UpdateAsync(
            id,
            setup => setup.AssignActingCapacity(positionId, actingCapacityId),
            cancellationToken);

    public async Task<Result<OrganizationSetupDto>> CreateNextVersionAsync(
        Guid id,
        OrganizationSetupActionInput action,
        CancellationToken cancellationToken = default)
    {
        var contextResult = CreateActionContext(action);
        if (contextResult.IsFailure)
            return Result<OrganizationSetupDto>.Failure(contextResult.Error);

        var current = await FindAsync(id, cancellationToken);
        if (current is null)
            return Result<OrganizationSetupDto>.Failure(OrganizationSetupApplicationErrors.NotFound);

        var versionResult = current.CreateNextVersion(contextResult.Value);
        if (versionResult.IsFailure)
            return Result<OrganizationSetupDto>.Failure(versionResult.Error);

        var added = await _repository.TryAddVersionAsync(
            current, versionResult.Value, cancellationToken);
        if (!added)
            return Result<OrganizationSetupDto>.Failure(OrganizationSetupApplicationErrors.PersistenceConflict);

        return Result<OrganizationSetupDto>.Success(
            (await MapAsync(versionResult.Value, cancellationToken))!);
    }

    public async Task<Result<OrganizationSetupDto>> PublishAsync(
        Guid id,
        OrganizationSetupActionInput action,
        CancellationToken cancellationToken = default)
    {
        var contextResult = CreateActionContext(action);
        if (contextResult.IsFailure)
            return Result<OrganizationSetupDto>.Failure(contextResult.Error);

        var setup = await FindAsync(id, cancellationToken);
        if (setup is null)
            return Result<OrganizationSetupDto>.Failure(OrganizationSetupApplicationErrors.NotFound);

        var published = await _repository.GetPublishedAsync(
            setup.OrganizationId, cancellationToken);
        var result = setup.Publish(published, contextResult.Value);
        if (result.IsFailure)
            return Result<OrganizationSetupDto>.Failure(result.Error);

        var saved = await _repository.TrySaveChangesAsync(cancellationToken);
        if (!saved)
            return Result<OrganizationSetupDto>.Failure(OrganizationSetupApplicationErrors.PersistenceConflict);

        return Result<OrganizationSetupDto>.Success((await MapAsync(setup, cancellationToken))!);
    }

    private async Task<Result<OrganizationSetupDto>> UpdateAsync(
        Guid id,
        Func<OrganizationSetupAggregate, Result> operation,
        CancellationToken cancellationToken)
    {
        var setup = await FindAsync(id, cancellationToken);
        if (setup is null)
            return Result<OrganizationSetupDto>.Failure(OrganizationSetupApplicationErrors.NotFound);

        var result = operation(setup);
        if (result.IsFailure)
            return Result<OrganizationSetupDto>.Failure(result.Error);

        var saved = await _repository.TrySaveChangesAsync(cancellationToken);
        if (!saved)
            return Result<OrganizationSetupDto>.Failure(OrganizationSetupApplicationErrors.PersistenceConflict);

        return Result<OrganizationSetupDto>.Success((await MapAsync(setup, cancellationToken))!);
    }

    private Task<OrganizationSetupAggregate?> FindAsync(Guid id, CancellationToken cancellationToken)
        => _repository.GetByIdAsync(OrganizationSetupId.From(id), cancellationToken);

    private async Task<OrganizationSetupDto?> MapAsync(
        OrganizationSetupAggregate setup,
        CancellationToken cancellationToken)
    {
        var organization = await _repository.GetOrganizationAsync(
            setup.OrganizationId, cancellationToken);
        return organization is null ? null : OrganizationSetupDto.From(organization, setup);
    }

    private static Result<Domain.Aggregates.OrganizationSetup.OrganizationSetupActionContext>
        CreateActionContext(OrganizationSetupActionInput action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var result = Domain.Aggregates.OrganizationSetup.OrganizationSetupActionContext.Create(
            action.ActorReference,
            action.PolicyVersion,
            action.AuthorizationReference,
            DateTime.UtcNow);
        return result.IsSuccess
            ? result
            : Result<Domain.Aggregates.OrganizationSetup.OrganizationSetupActionContext>.Failure(
                OrganizationSetupApplicationErrors.InvalidAction);
    }

    private static bool IsValid(string? value, int maximumLength)
        => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= maximumLength;
}

public sealed record OrganizationSetupActionInput(
    string? ActorReference,
    string? PolicyVersion,
    string? AuthorizationReference);
