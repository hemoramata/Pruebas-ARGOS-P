using ARGOS.Domain.Aggregates.CaseFile.Enums;
using ARGOS.Domain.Aggregates.CaseFile.Errors;
using ARGOS.Domain.Aggregates.CaseFile.Events;
using ARGOS.Domain.Aggregates.CaseFile.ValueObjects;
using ARGOS.Domain.Aggregates.Evidence.ValueObjects;
using ARGOS.SharedKernel.Abstractions;
using ARGOS.SharedKernel.Results;

namespace ARGOS.Domain.Aggregates.CaseFile;

/// <summary>
/// Representa el Aggregate Root del Expediente.
///
/// El expediente es la unidad mínima de gestión dentro de ARGOS.
/// Sobre él se registran documentos, evidencias, trámites,
/// participantes y toda la memoria institucional asociada.
/// </summary>
public sealed class CaseFile : AggregateRoot<Guid>
{
    private readonly List<CaseFileEvidence> _evidenceLinks = new();

    /// <summary>
    /// Número institucional del expediente.
    /// </summary>
    public CaseFileNumber Number { get; private set; }

    /// <summary>
    /// Estado actual del expediente.
    /// </summary>
    public CaseFileStatus Status { get; private set; }

    /// <summary>
    /// Reason that currently prevents the case file from continuing.
    /// It is populated only while the case file is waiting.
    /// </summary>
    public string? WaitingReason { get; private set; }

    /// <summary>
    /// Code of the definitive procedure result that finalized the case file.
    /// </summary>
    public string? FinalResultCode { get; private set; }
    public string? SubjectTypeCode { get; private set; }
    public string? SubjectReference { get; private set; }
    public Guid? ProcedureVersionId { get; private set; }

    /// <summary>
    /// Traceable evidence usages belonging to this case file.
    /// </summary>
    public IReadOnlyCollection<CaseFileEvidence> EvidenceLinks =>
        _evidenceLinks.AsReadOnly();

    /// <summary>
    /// Constructor requerido por EF Core.
    /// </summary>
    private CaseFile()
    {
        Number = null!;
    }

    /// <summary>
    /// Crea un nuevo expediente.
    /// </summary>
    /// <param name="id">Identificador único del expediente.</param>
    /// <param name="number">Número institucional del expediente.</param>
    public CaseFile(Guid id, CaseFileNumber number, string? subjectTypeCode = null, string? subjectReference = null, Guid? procedureVersionId = null)
        : base(id)
    {
        ArgumentNullException.ThrowIfNull(number);
        if (subjectTypeCode is not null || subjectReference is not null || procedureVersionId is not null)
        {
            if (string.IsNullOrWhiteSpace(subjectTypeCode) || subjectTypeCode.Trim().Length > 100
                || string.IsNullOrWhiteSpace(subjectReference) || subjectReference.Trim().Length > 200
                || procedureVersionId is null || procedureVersionId == Guid.Empty) throw new ArgumentException("Incomplete case subject.");
            SubjectTypeCode = subjectTypeCode.Trim().ToUpperInvariant();
            SubjectReference = subjectReference.Trim();
            ProcedureVersionId = procedureVersionId;
        }

        Number = number;
        Status = CaseFileStatus.Created;

        AddDomainEvent(new CaseFileCreatedDomainEvent(
            Id,
            Number.Value));
    }

    /// <summary>
    /// Starts the institutional route of a newly created case file.
    /// </summary>
    public Result StartProcessing()
    {
        return ChangeStatus(
            CaseFileStatus.Created,
            CaseFileStatus.InProgress,
            "Institutional route started.");
    }

    /// <summary>
    /// Places an active case file in a waiting state and records why it cannot continue.
    /// </summary>
    public Result PutOnHold(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure(CaseFileErrors.WaitingReasonRequired);
        if (reason.Trim().Length > 2000)
            return Result.Failure(CaseFileErrors.WaitingReasonTooLong);

        var result = ChangeStatus(
            CaseFileStatus.InProgress,
            CaseFileStatus.Waiting,
            reason.Trim());

        if (result.IsSuccess)
            WaitingReason = reason.Trim();

        return result;
    }

    /// <summary>
    /// Resumes the route after the pending action has been resolved.
    /// </summary>
    public Result ResumeProcessing()
    {
        var result = ChangeStatus(
            CaseFileStatus.Waiting,
            CaseFileStatus.InProgress,
            "Pending action resolved.");

        if (result.IsSuccess)
            WaitingReason = null;

        return result;
    }

    /// <summary>
    /// Finalizes the case file with a definitive, configured procedure result.
    /// </summary>
    public Result FinalizeWithResult(string resultCode)
    {
        if (string.IsNullOrWhiteSpace(resultCode))
            return Result.Failure(CaseFileErrors.FinalResultRequired);
        if (resultCode.Trim().Length > 100)
            return Result.Failure(CaseFileErrors.FinalResultTooLong);

        if (Status is not CaseFileStatus.InProgress and not CaseFileStatus.Waiting)
        {
            return Result.Failure(CaseFileErrors.InvalidTransition(
                Status,
                CaseFileStatus.Finalized));
        }

        var previousStatus = Status;
        var normalizedResultCode = resultCode.Trim().ToUpperInvariant();

        Status = CaseFileStatus.Finalized;
        WaitingReason = null;
        FinalResultCode = normalizedResultCode;

        RaiseDomainEvent(new CaseFileStatusChangedDomainEvent(
            Id,
            previousStatus,
            Status,
            $"Final result: {normalizedResultCode}"));

        return Result.Success();
    }

    /// <summary>
    /// Moves a finalized case file to historical custody.
    /// </summary>
    public Result Archive()
    {
        return ChangeStatus(
            CaseFileStatus.Finalized,
            CaseFileStatus.Archived,
            "Case file transferred to historical custody.");
    }

    /// <summary>
    /// Links reusable institutional evidence without copying or modifying it.
    /// </summary>
    public Result<CaseFileEvidence> LinkEvidence(
        EvidenceId? evidenceId,
        string? requirementCode,
        Guid procedureVersionId,
        string? policyVersion,
        Guid linkedByPositionId,
        DateTimeOffset validatedAtUtc,
        DateTimeOffset linkedAtUtc)
    {
        if (Status is CaseFileStatus.Finalized or CaseFileStatus.Archived)
        {
            return Result<CaseFileEvidence>.Failure(
                CaseFileErrors.EvidenceLinkNotAllowed);
        }

        if (evidenceId is null
            || string.IsNullOrWhiteSpace(requirementCode)
            || requirementCode.Trim().Length > 100
            || procedureVersionId == Guid.Empty
            || string.IsNullOrWhiteSpace(policyVersion)
            || policyVersion.Trim().Length > 100
            || linkedByPositionId == Guid.Empty)
        {
            return Result<CaseFileEvidence>.Failure(
                CaseFileErrors.InvalidEvidenceLink);
        }

        if (linkedAtUtc < validatedAtUtc)
        {
            return Result<CaseFileEvidence>.Failure(
                CaseFileErrors.InvalidEvidenceLinkDates);
        }

        var normalizedRequirementCode = requirementCode.Trim().ToUpperInvariant();
        var duplicate = _evidenceLinks.Any(link =>
            link.EvidenceId == evidenceId
            && link.RequirementCode == normalizedRequirementCode);

        if (duplicate)
        {
            return Result<CaseFileEvidence>.Failure(
                CaseFileErrors.DuplicateEvidenceLink);
        }

        var link = new CaseFileEvidence(
            evidenceId,
            normalizedRequirementCode,
            procedureVersionId,
            policyVersion,
            linkedByPositionId,
            validatedAtUtc,
            linkedAtUtc);

        _evidenceLinks.Add(link);
        RaiseDomainEvent(new CaseFileEvidenceLinkedDomainEvent(
            Id,
            link.Id,
            evidenceId,
            normalizedRequirementCode));

        return Result<CaseFileEvidence>.Success(link);
    }

    private Result ChangeStatus(
        CaseFileStatus requiredStatus,
        CaseFileStatus newStatus,
        string reason)
    {
        if (Status != requiredStatus)
            return Result.Failure(CaseFileErrors.InvalidTransition(Status, newStatus));

        var previousStatus = Status;
        Status = newStatus;

        RaiseDomainEvent(new CaseFileStatusChangedDomainEvent(
            Id,
            previousStatus,
            newStatus,
            reason));

        return Result.Success();
    }
}
