using ARGOS.Domain.Aggregates.Evidence.ValueObjects;
using ARGOS.SharedKernel.Abstractions;

namespace ARGOS.Domain.Aggregates.CaseFile;

/// <summary>
/// Immutable record of how a case file used an institutional evidence item.
/// </summary>
public sealed class CaseFileEvidence : Entity<Guid>
{
    public EvidenceId EvidenceId { get; private set; } = null!;
    public string RequirementCode { get; private set; } = string.Empty;
    public Guid ProcedureVersionId { get; private set; }
    public string PolicyVersion { get; private set; } = string.Empty;
    public Guid LinkedByPositionId { get; private set; }
    public DateTimeOffset ValidatedAtUtc { get; private set; }
    public DateTimeOffset LinkedAtUtc { get; private set; }

    private CaseFileEvidence()
    {
    }

    internal CaseFileEvidence(
        EvidenceId evidenceId,
        string requirementCode,
        Guid procedureVersionId,
        string policyVersion,
        Guid linkedByPositionId,
        DateTimeOffset validatedAtUtc,
        DateTimeOffset linkedAtUtc)
        : base(Guid.NewGuid())
    {
        EvidenceId = evidenceId;
        RequirementCode = requirementCode.Trim().ToUpperInvariant();
        ProcedureVersionId = procedureVersionId;
        PolicyVersion = policyVersion.Trim();
        LinkedByPositionId = linkedByPositionId;
        ValidatedAtUtc = validatedAtUtc;
        LinkedAtUtc = linkedAtUtc;
    }
}
