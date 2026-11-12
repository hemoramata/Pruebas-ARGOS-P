using ARGOS.Domain.Aggregates.Organization.ValueObjects;
using ARGOS.Domain.Aggregates.OrganizationSetup.Enums;
using ARGOS.Domain.Aggregates.OrganizationSetup.Errors;
using ARGOS.Domain.Aggregates.OrganizationSetup.Events;
using ARGOS.Domain.Aggregates.OrganizationSetup.ValueObjects;
using ARGOS.SharedKernel.Abstractions;
using ARGOS.SharedKernel.Results;

namespace ARGOS.Domain.Aggregates.OrganizationSetup;

/// <summary>
/// Versioned organizational vocabulary and catalog configured for one organization.
/// </summary>
public sealed class OrganizationSetup : AggregateRoot<OrganizationSetupId>
{
    private readonly List<OrganizationTerm> _terms = [];
    private readonly List<OrganizationUnitDefinition> _units = [];
    private readonly List<InstitutionalPositionDefinition> _positions = [];
    private readonly List<CompetenceDefinition> _competences = [];
    private readonly List<ActingCapacityDefinition> _actingCapacities = [];

    private OrganizationSetup()
    {
    }

    private OrganizationSetup(
        OrganizationSetupId id,
        OrganizationId organizationId,
        int version,
        OrganizationSetupId? previousVersionId,
        OrganizationSetupActionContext context)
        : base(id)
    {
        OrganizationId = organizationId;
        Version = version;
        PreviousVersionId = previousVersionId;
        Status = OrganizationSetupStatus.Draft;
        CreatedByReference = context.ActorReference;
        CreationPolicyVersion = context.PolicyVersion;
        CreationAuthorizationReference = context.AuthorizationReference;
        CreatedAtUtc = context.OccurredAtUtc;

        RaiseDomainEvent(new OrganizationSetupCreatedEvent(
            Id,
            OrganizationId,
            Version,
            context.ActorReference,
            context.OccurredAtUtc));
    }

    public OrganizationId OrganizationId { get; private set; } = null!;

    public int Version { get; private set; }

    public OrganizationSetupId? PreviousVersionId { get; private set; }

    public OrganizationSetupId? NextVersionId { get; private set; }

    public OrganizationSetupStatus Status { get; private set; }

    public string CreatedByReference { get; private set; } = string.Empty;

    public string CreationPolicyVersion { get; private set; } = string.Empty;

    public string CreationAuthorizationReference { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; private set; }

    public string? PublishedByReference { get; private set; }

    public string? PublicationAuthorizationReference { get; private set; }

    public DateTime? PublishedAtUtc { get; private set; }

    public IReadOnlyCollection<OrganizationTerm> Terms => _terms.AsReadOnly();

    public IReadOnlyCollection<OrganizationUnitDefinition> Units => _units.AsReadOnly();

    public IReadOnlyCollection<InstitutionalPositionDefinition> Positions =>
        _positions.AsReadOnly();

    public IReadOnlyCollection<CompetenceDefinition> Competences =>
        _competences.AsReadOnly();

    public IReadOnlyCollection<ActingCapacityDefinition> ActingCapacities =>
        _actingCapacities.AsReadOnly();

    public static Result<OrganizationSetup> CreateInitial(
        OrganizationId? organizationId,
        OrganizationSetupActionContext? context)
    {
        if (organizationId is null || context is null)
        {
            return Result<OrganizationSetup>.Failure(
                OrganizationSetupErrors.InvalidActionContext);
        }

        return Result<OrganizationSetup>.Success(new OrganizationSetup(
            OrganizationSetupId.New(),
            organizationId,
            version: 1,
            previousVersionId: null,
            context));
    }

    public Result SetTerm(
        OrganizationConcept concept,
        string? singular,
        string? plural)
    {
        var draftResult = EnsureDraft();
        if (draftResult.IsFailure)
            return draftResult;

        if (!IsValidName(singular) || !IsValidName(plural))
            return Result.Failure(OrganizationSetupErrors.InvalidCatalogItem);

        var existing = _terms.SingleOrDefault(term => term.Concept == concept);
        if (existing is null)
        {
            _terms.Add(new OrganizationTerm(
                concept,
                singular!.Trim(),
                plural!.Trim()));
        }
        else
        {
            existing.ChangeLabels(singular!.Trim(), plural!.Trim());
        }

        return Result.Success();
    }

    public Result<Guid> AddUnit(
        string? code,
        string? name,
        Guid? parentUnitId = null)
    {
        var validation = ValidateNewCatalogItem(code, name, _units.Select(unit => unit.Code));
        if (validation.IsFailure)
            return Result<Guid>.Failure(validation.Error);

        if (parentUnitId.HasValue && _units.All(unit => unit.Id != parentUnitId.Value))
            return Result<Guid>.Failure(OrganizationSetupErrors.UnitNotFound);

        var id = Guid.NewGuid();
        _units.Add(new OrganizationUnitDefinition(
            id,
            NormalizeCode(code!),
            name!.Trim(),
            parentUnitId));

        return Result<Guid>.Success(id);
    }

    public Result RenameUnit(Guid unitId, string? name)
    {
        var draftResult = EnsureDraft();
        if (draftResult.IsFailure)
            return draftResult;

        if (!IsValidName(name))
            return Result.Failure(OrganizationSetupErrors.InvalidCatalogItem);

        var unit = _units.SingleOrDefault(item => item.Id == unitId);
        if (unit is null)
            return Result.Failure(OrganizationSetupErrors.UnitNotFound);

        unit.Rename(name!.Trim());
        return Result.Success();
    }

    public Result RemoveUnit(Guid unitId)
    {
        var draftResult = EnsureDraft();
        if (draftResult.IsFailure) return draftResult;
        var unit = _units.SingleOrDefault(item => item.Id == unitId);
        if (unit is null) return Result.Failure(OrganizationSetupErrors.UnitNotFound);
        if (_units.Any(item => item.ParentUnitId == unitId)
            || _positions.Any(position => position.OrganizationUnitId == unitId))
            return Result.Failure(OrganizationSetupErrors.UnitHasDependants);
        _units.Remove(unit);
        return Result.Success();
    }

    public Result<Guid> AddCompetence(
        string? code,
        string? name,
        string? description = null)
    {
        var validation = ValidateNewCatalogItem(
            code,
            name,
            _competences.Select(item => item.Code));
        if (validation.IsFailure)
            return Result<Guid>.Failure(validation.Error);

        if (description?.Length > 2000)
            return Result<Guid>.Failure(OrganizationSetupErrors.InvalidCatalogItem);

        var id = Guid.NewGuid();
        _competences.Add(new CompetenceDefinition(
            id,
            NormalizeCode(code!),
            name!.Trim(),
            NormalizeDescription(description)));

        return Result<Guid>.Success(id);
    }

    public Result<Guid> AddActingCapacity(
        string? code,
        string? name,
        string? description = null)
    {
        var validation = ValidateNewCatalogItem(
            code,
            name,
            _actingCapacities.Select(item => item.Code));
        if (validation.IsFailure)
            return Result<Guid>.Failure(validation.Error);

        if (description?.Length > 2000)
            return Result<Guid>.Failure(OrganizationSetupErrors.InvalidCatalogItem);

        var id = Guid.NewGuid();
        _actingCapacities.Add(new ActingCapacityDefinition(
            id,
            NormalizeCode(code!),
            name!.Trim(),
            NormalizeDescription(description)));

        return Result<Guid>.Success(id);
    }

    public Result<Guid> AddPosition(
        string? code,
        string? name,
        Guid organizationUnitId)
    {
        var validation = ValidateNewCatalogItem(
            code,
            name,
            _positions.Select(item => item.Code));
        if (validation.IsFailure)
            return Result<Guid>.Failure(validation.Error);

        if (_units.All(unit => unit.Id != organizationUnitId))
            return Result<Guid>.Failure(OrganizationSetupErrors.UnitNotFound);

        var id = Guid.NewGuid();
        _positions.Add(new InstitutionalPositionDefinition(
            id,
            NormalizeCode(code!),
            name!.Trim(),
            organizationUnitId));

        return Result<Guid>.Success(id);
    }

    public Result RenamePosition(Guid positionId, string? name)
    {
        var draftResult = EnsureDraft();
        if (draftResult.IsFailure)
            return draftResult;

        if (!IsValidName(name))
            return Result.Failure(OrganizationSetupErrors.InvalidCatalogItem);

        var position = _positions.SingleOrDefault(item => item.Id == positionId);
        if (position is null)
            return Result.Failure(OrganizationSetupErrors.PositionNotFound);

        position.Rename(name!.Trim());
        return Result.Success();
    }

    public Result AssignCompetence(Guid positionId, Guid competenceId)
    {
        var draftResult = EnsureDraft();
        if (draftResult.IsFailure)
            return draftResult;

        var position = _positions.SingleOrDefault(item => item.Id == positionId);
        if (position is null)
            return Result.Failure(OrganizationSetupErrors.PositionNotFound);

        if (_competences.All(item => item.Id != competenceId))
            return Result.Failure(OrganizationSetupErrors.CompetenceNotFound);

        position.AddCompetence(competenceId);
        return Result.Success();
    }

    public Result AssignActingCapacity(Guid positionId, Guid actingCapacityId)
    {
        var draftResult = EnsureDraft();
        if (draftResult.IsFailure)
            return draftResult;

        var position = _positions.SingleOrDefault(item => item.Id == positionId);
        if (position is null)
            return Result.Failure(OrganizationSetupErrors.PositionNotFound);

        if (_actingCapacities.All(item => item.Id != actingCapacityId))
            return Result.Failure(OrganizationSetupErrors.ActingCapacityNotFound);

        position.AddActingCapacity(actingCapacityId);
        return Result.Success();
    }

    public Result<OrganizationSetup> CreateNextVersion(
        OrganizationSetupActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (Status != OrganizationSetupStatus.Published)
        {
            return Result<OrganizationSetup>.Failure(
                OrganizationSetupErrors.PublishedVersionRequired);
        }

        if (NextVersionId is not null)
        {
            return Result<OrganizationSetup>.Failure(
                OrganizationSetupErrors.VersionAlreadyHasSuccessor);
        }

        var next = new OrganizationSetup(
            OrganizationSetupId.New(),
            OrganizationId,
            Version + 1,
            Id,
            context);

        next._terms.AddRange(_terms.Select(item => item.Clone()));
        next._units.AddRange(_units.Select(item => item.Clone()));
        next._positions.AddRange(_positions.Select(item => item.Clone()));
        next._competences.AddRange(_competences.Select(item => item.Clone()));
        next._actingCapacities.AddRange(_actingCapacities.Select(item => item.Clone()));

        NextVersionId = next.Id;
        return Result<OrganizationSetup>.Success(next);
    }

    public Result Publish(
        OrganizationSetup? currentlyPublished,
        OrganizationSetupActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var draftResult = EnsureDraft();
        if (draftResult.IsFailure)
            return draftResult;

        if (!IsComplete())
            return Result.Failure(OrganizationSetupErrors.IncompleteConfiguration);

        if (_positions.Any(position => position.CompetenceIds.Count == 0))
            return Result.Failure(OrganizationSetupErrors.PositionWithoutCompetence);

        if ((Version == 1 && currentlyPublished is not null)
            || (Version > 1
                && (currentlyPublished is null
                    || PreviousVersionId is null
                    || !currentlyPublished.Id.Equals(PreviousVersionId))))
        {
            return Result.Failure(OrganizationSetupErrors.InvalidPublishedPredecessor);
        }

        if (currentlyPublished is not null)
        {
            if (currentlyPublished == this
                || !currentlyPublished.OrganizationId.Equals(OrganizationId)
                || currentlyPublished.Status != OrganizationSetupStatus.Published)
            {
                return Result.Failure(OrganizationSetupErrors.InvalidPublishedPredecessor);
            }

            currentlyPublished.Status = OrganizationSetupStatus.Retired;
        }

        Status = OrganizationSetupStatus.Published;
        PublishedByReference = context.ActorReference;
        PublicationAuthorizationReference = context.AuthorizationReference;
        PublishedAtUtc = context.OccurredAtUtc;

        RaiseDomainEvent(new OrganizationSetupPublishedEvent(
            Id,
            OrganizationId,
            Version,
            currentlyPublished?.Id,
            context.ActorReference,
            context.AuthorizationReference,
            context.OccurredAtUtc));

        return Result.Success();
    }

    private Result ValidateNewCatalogItem(
        string? code,
        string? name,
        IEnumerable<string> existingCodes)
    {
        var draftResult = EnsureDraft();
        if (draftResult.IsFailure)
            return draftResult;

        if (!IsValidCode(code) || !IsValidName(name))
            return Result.Failure(OrganizationSetupErrors.InvalidCatalogItem);

        var normalizedCode = NormalizeCode(code!);
        if (existingCodes.Any(existing =>
            string.Equals(existing, normalizedCode, StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Failure(OrganizationSetupErrors.DuplicateCode);
        }

        return Result.Success();
    }

    private Result EnsureDraft()
        => Status == OrganizationSetupStatus.Draft
            ? Result.Success()
            : Result.Failure(OrganizationSetupErrors.DraftRequired);

    private bool IsComplete()
        => Enum.GetValues<OrganizationConcept>()
            .All(concept => _terms.Any(term => term.Concept == concept))
            && _units.Count > 0
            && _positions.Count > 0
            && _competences.Count > 0
            && _actingCapacities.Count > 0;

    private static bool IsValidCode(string? value)
        => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= 50;

    private static bool IsValidName(string? value)
        => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= 200;

    private static string NormalizeCode(string value)
        => value.Trim().ToUpperInvariant();

    private static string? NormalizeDescription(string? description)
        => string.IsNullOrWhiteSpace(description) ? null : description.Trim();
}
