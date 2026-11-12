using ARGOS.Domain.Aggregates.Evidence.Enums;
using ARGOS.Domain.Aggregates.Evidence.Errors;
using ARGOS.Domain.Aggregates.Evidence.Events;
using ARGOS.Domain.Aggregates.Evidence.ValueObjects;
using ARGOS.SharedKernel.Abstractions;
using ARGOS.SharedKernel.Results;

namespace ARGOS.Domain.Aggregates.Evidence;

/// <summary>
/// Institutional information capable of supporting an action or resolution.
/// Evidence is independent from a particular case file and may be reused through
/// a traceable CaseFileEvidence link.
/// </summary>
public sealed class Evidence : AggregateRoot<EvidenceId>
{
    private readonly List<EvidenceSource> _sources = new();

    public Guid CustodianOrganizationId { get; private set; }
    public string EvidenceTypeCode { get; private set; } = string.Empty;
    public string SubjectTypeCode { get; private set; } = string.Empty;
    public string SubjectReference { get; private set; } = string.Empty;
    public EvidenceStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public Guid? ProtectedContentId { get; private set; }
    public IReadOnlyCollection<EvidenceSource> Sources => _sources.AsReadOnly();

    private Evidence()
    {
    }

    private Evidence(
        EvidenceId id,
        Guid custodianOrganizationId,
        string evidenceTypeCode,
        string subjectTypeCode,
        string subjectReference,
        EvidenceSource initialSource,
        DateTimeOffset createdAtUtc,
        Guid? protectedContentId = null)
        : base(id)
    {
        CustodianOrganizationId = custodianOrganizationId;
        EvidenceTypeCode = evidenceTypeCode.Trim().ToUpperInvariant();
        SubjectTypeCode = subjectTypeCode.Trim().ToUpperInvariant();
        SubjectReference = subjectReference.Trim();
        Status = EvidenceStatus.Active;
        CreatedAtUtc = createdAtUtc;
        ProtectedContentId = protectedContentId;
        _sources.Add(initialSource);

        RaiseDomainEvent(new EvidenceCreatedDomainEvent(
            Id,
            CustodianOrganizationId,
            EvidenceTypeCode));
    }

    public static Result<Evidence> Create(
        Guid custodianOrganizationId,
        string? evidenceTypeCode,
        string? subjectTypeCode,
        string? subjectReference,
        EvidenceSource? initialSource,
        DateTimeOffset createdAtUtc,
        Guid? protectedContentId = null)
    {
        if (custodianOrganizationId == Guid.Empty
            || string.IsNullOrWhiteSpace(evidenceTypeCode)
            || evidenceTypeCode.Trim().Length > 100
            || string.IsNullOrWhiteSpace(subjectTypeCode)
            || subjectTypeCode.Trim().Length > 100
            || string.IsNullOrWhiteSpace(subjectReference)
            || subjectReference.Trim().Length > 200
            || initialSource is null)
        {
            return Result<Evidence>.Failure(EvidenceErrors.InvalidEvidenceData);
        }

        return Result<Evidence>.Success(new Evidence(
            EvidenceId.New(),
            custodianOrganizationId,
            evidenceTypeCode,
            subjectTypeCode,
            subjectReference,
            initialSource,
            createdAtUtc, protectedContentId));
    }

    public Result AddSource(EvidenceSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (Status != EvidenceStatus.Active)
            return Result.Failure(EvidenceErrors.InactiveEvidenceCannotChange);

        if (_sources.Any(existing => existing.Id == source.Id))
            return Result.Failure(EvidenceErrors.DuplicateSource);

        _sources.Add(source);
        RaiseDomainEvent(new EvidenceSourceAddedDomainEvent(Id, source.Id));
        return Result.Success();
    }
}
