using ARGOS.Application.Common.Persistence;
using ARGOS.Domain.Aggregates.Procedure;
using ARGOS.Domain.Aggregates.Procedure.ValueObjects;
using ARGOS.SharedKernel.Results;

namespace ARGOS.Application.Modules.Procedures;

public sealed class ProcedureService
{
    private readonly IProcedureRepository _repository;

    public ProcedureService(IProcedureRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyCollection<ProcedureDto>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var procedures = await _repository.ListPublishedAsync(cancellationToken);
        return procedures.Select(ProcedureDto.From).ToArray();
    }

    public async Task<ProcedureDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var procedure = await _repository.GetByIdAsync(
            ProcedureId.From(id),
            cancellationToken);

        return procedure is null ? null : ProcedureDto.From(procedure);
    }

    public async Task<Result<ProcedureDto>> CreateAsync(
        string? code,
        string? name,
        string? description,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code)
            || code.Trim().Length > 50
            || string.IsNullOrWhiteSpace(name)
            || name.Trim().Length > 200
            || string.IsNullOrWhiteSpace(description) || description.Trim().Length > 2000)
        {
            return Result<ProcedureDto>.Failure(ProcedureErrors.InvalidData);
        }

        var procedure = Procedure.Create(code, name, description);
        var added = await _repository.TryAddAsync(procedure, cancellationToken);

        return added
            ? Result<ProcedureDto>.Success(ProcedureDto.From(procedure))
            : Result<ProcedureDto>.Failure(ProcedureErrors.DuplicateCode);
    }

    public async Task<Result<ProcedureDto>> CreateVersionAsync(
        Guid id,
        string? name,
        string? description,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name)
            || name.Trim().Length > 200
            || string.IsNullOrWhiteSpace(description) || description.Trim().Length > 2000)
        {
            return Result<ProcedureDto>.Failure(ProcedureErrors.InvalidData);
        }

        var procedureId = ProcedureId.From(id);
        var procedure = await _repository.GetByIdAsync(
            procedureId,
            cancellationToken);

        if (procedure is null)
        {
            return Result<ProcedureDto>.Failure(ProcedureErrors.NotFound);
        }

        var versionResult = procedure.CreateNewVersion(name, description);
        if (versionResult.IsFailure)
            return Result<ProcedureDto>.Failure(versionResult.Error);

        var newVersion = versionResult.Value;
        var added = await _repository.TryAddVersionAsync(
            procedure,
            newVersion,
            cancellationToken);

        if (!added)
            return Result<ProcedureDto>.Failure(ProcedureErrors.VersionConflict);

        return Result<ProcedureDto>.Success(ProcedureDto.From(newVersion));
    }

    public async Task<Result<IReadOnlyCollection<ProcedureDto>>> ListVersionsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var procedure = await _repository.GetByIdAsync(
            ProcedureId.From(id),
            cancellationToken);

        if (procedure is null)
        {
            return Result<IReadOnlyCollection<ProcedureDto>>.Failure(
                ProcedureErrors.NotFound);
        }

        var versions = await _repository.ListVersionsAsync(
            procedure.DefinitionId,
            cancellationToken);

        IReadOnlyCollection<ProcedureDto> result = versions
            .Select(ProcedureDto.From)
            .ToArray();

        return Result<IReadOnlyCollection<ProcedureDto>>.Success(result);
    }

    public async Task<Result<ProcedureDto>> SubmitForReviewAsync(
        Guid id,
        ProcedureApprovalPolicyInput policy,
        ProcedureDecisionInput decision,
        CancellationToken cancellationToken = default)
    {
        var contextResult = CreateDecisionContext(decision);
        if (contextResult.IsFailure)
            return Result<ProcedureDto>.Failure(contextResult.Error);

        var procedure = await FindAsync(id, cancellationToken);

        if (procedure is null)
            return Result<ProcedureDto>.Failure(ProcedureErrors.NotFound);

        var result = procedure.SubmitForReview(
            policy.RequiredApprovalCount,
            policy.SeparationOfDutiesRequired,
            policy.EmergencyPublicationAllowed,
            policy.EmergencyRatificationRequired,
            contextResult.Value);

        return await SaveResultAsync(procedure, result, cancellationToken);
    }

    public Task<Result<ProcedureDto>> ApproveAsync(
        Guid id,
        ProcedureDecisionInput decision,
        CancellationToken cancellationToken = default)
        => ApplyDecisionAsync(id, decision, static (procedure, context) =>
            procedure.Approve(context), cancellationToken);

    public Task<Result<ProcedureDto>> RejectAsync(
        Guid id,
        ProcedureDecisionInput decision,
        CancellationToken cancellationToken = default)
        => ApplyDecisionAsync(id, decision, static (procedure, context) =>
            procedure.Reject(context), cancellationToken);

    public async Task<Result<ProcedureDto>> PublishAsync(
        Guid id,
        ProcedureDecisionInput decision,
        CancellationToken cancellationToken = default)
    {
        var contextResult = CreateDecisionContext(decision);
        if (contextResult.IsFailure)
            return Result<ProcedureDto>.Failure(contextResult.Error);

        var procedure = await FindAsync(id, cancellationToken);
        if (procedure is null)
            return Result<ProcedureDto>.Failure(ProcedureErrors.NotFound);

        var currentlyPublished = await _repository.GetPublishedAsync(
            procedure.DefinitionId,
            cancellationToken);

        var result = procedure.Publish(currentlyPublished, contextResult.Value);
        return await SaveResultAsync(procedure, result, cancellationToken);
    }

    public async Task<Result<ProcedureDto>> PublishEmergencyAsync(
        Guid id,
        DateTime ratificationDueAtUtc,
        ProcedureDecisionInput decision,
        CancellationToken cancellationToken = default)
    {
        var contextResult = CreateDecisionContext(decision);
        if (contextResult.IsFailure)
            return Result<ProcedureDto>.Failure(contextResult.Error);

        var procedure = await FindAsync(id, cancellationToken);
        if (procedure is null)
            return Result<ProcedureDto>.Failure(ProcedureErrors.NotFound);

        var currentlyPublished = await _repository.GetPublishedAsync(
            procedure.DefinitionId,
            cancellationToken);

        var result = procedure.PublishEmergency(
            currentlyPublished,
            contextResult.Value,
            ratificationDueAtUtc);

        return await SaveResultAsync(procedure, result, cancellationToken);
    }

    public Task<Result<ProcedureDto>> RatifyEmergencyAsync(
        Guid id,
        ProcedureDecisionInput decision,
        CancellationToken cancellationToken = default)
        => ApplyDecisionAsync(id, decision, static (procedure, context) =>
            procedure.RatifyEmergencyPublication(context), cancellationToken);

    public Task<Result<ProcedureDto>> RetireAsync(
        Guid id,
        ProcedureDecisionInput decision,
        CancellationToken cancellationToken = default)
        => ApplyDecisionAsync(id, decision, static (procedure, context) =>
            procedure.Retire(context), cancellationToken);

    public Task<Result<ProcedureDto>> ArchiveAsync(
        Guid id,
        ProcedureDecisionInput decision,
        CancellationToken cancellationToken = default)
        => ApplyDecisionAsync(id, decision, static (procedure, context) =>
            procedure.Archive(context), cancellationToken);

    private async Task<Result<ProcedureDto>> ApplyDecisionAsync(
        Guid id,
        ProcedureDecisionInput decision,
        Func<Procedure, ProcedureDecisionContext, Result> operation,
        CancellationToken cancellationToken)
    {
        var contextResult = CreateDecisionContext(decision);
        if (contextResult.IsFailure)
            return Result<ProcedureDto>.Failure(contextResult.Error);

        var procedure = await FindAsync(id, cancellationToken);
        if (procedure is null)
            return Result<ProcedureDto>.Failure(ProcedureErrors.NotFound);

        var result = operation(procedure, contextResult.Value);
        return await SaveResultAsync(procedure, result, cancellationToken);
    }

    private Task<Procedure?> FindAsync(
        Guid id,
        CancellationToken cancellationToken)
        => _repository.GetByIdAsync(ProcedureId.From(id), cancellationToken);

    private static Result<ProcedureDecisionContext> CreateDecisionContext(
        ProcedureDecisionInput decision)
    {
        ArgumentNullException.ThrowIfNull(decision);

        var result = ProcedureDecisionContext.Create(
            decision.ActorReference,
            decision.AssignmentReference,
            decision.PositionReference,
            decision.CompetenceCode,
            decision.PolicyVersion,
            decision.AuthorizationReference,
            decision.Comments,
            DateTime.UtcNow);

        return result.IsSuccess
            ? result
            : Result<ProcedureDecisionContext>.Failure(
                ProcedureErrors.InvalidDecisionData);
    }

    private async Task<Result<ProcedureDto>> SaveResultAsync(
        Procedure procedure,
        Result result,
        CancellationToken cancellationToken)
    {
        if (result.IsFailure)
        {
            return Result<ProcedureDto>.Failure(result.Error);
        }

        var saved = await _repository.TrySaveChangesAsync(cancellationToken);

        return saved
            ? Result<ProcedureDto>.Success(ProcedureDto.From(procedure))
            : Result<ProcedureDto>.Failure(ProcedureErrors.VersionConflict);
    }
}

public sealed record ProcedureApprovalPolicyInput(
    int RequiredApprovalCount,
    bool SeparationOfDutiesRequired,
    bool EmergencyPublicationAllowed,
    bool EmergencyRatificationRequired);

public sealed record ProcedureDecisionInput(
    string? ActorReference,
    string? AssignmentReference,
    string? PositionReference,
    string? CompetenceCode,
    string? PolicyVersion,
    string? AuthorizationReference,
    string? Comments);
