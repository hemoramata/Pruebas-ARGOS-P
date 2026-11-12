using ARGOS.Domain.Aggregates.CaseFile.Enums;
using ARGOS.SharedKernel.Results;

namespace ARGOS.Domain.Aggregates.CaseFile.Errors;

/// <summary>
/// Business errors raised by the CaseFile aggregate.
/// </summary>
public static class CaseFileErrors
{
    public static readonly Error WaitingReasonTooLong = new(
        "CaseFile.WaitingReasonTooLong", "La razón de espera no puede superar 2000 caracteres.");

    public static readonly Error FinalResultTooLong = new(
        "CaseFile.FinalResultTooLong", "El código de resultado no puede superar 100 caracteres.");

    public static readonly Error WaitingReasonRequired = new(
        "CaseFile.WaitingReasonRequired",
        "A reason is required when a case file enters the waiting state.");

    public static readonly Error FinalResultRequired = new(
        "CaseFile.FinalResultRequired",
        "A definitive procedure result is required to finalize a case file.");

    public static readonly Error InvalidEvidenceLink = new(
        "CaseFile.InvalidEvidenceLink",
        "Evidence, requirement, procedure version, policy and linking position are required.");

    public static readonly Error DuplicateEvidenceLink = new(
        "CaseFile.DuplicateEvidenceLink",
        "The same evidence is already linked to this requirement in the case file.");

    public static readonly Error EvidenceLinkNotAllowed = new(
        "CaseFile.EvidenceLinkNotAllowed",
        "Evidence cannot be linked while the case file is finalized or archived.");

    public static readonly Error InvalidEvidenceLinkDates = new(
        "CaseFile.InvalidEvidenceLinkDates",
        "Evidence must be validated before or at the time it is linked.");

    public static Error InvalidTransition(
        CaseFileStatus currentStatus,
        CaseFileStatus requestedStatus)
    {
        return new Error(
            "CaseFile.InvalidStatusTransition",
            $"A case file cannot transition from {currentStatus} to {requestedStatus}.");
    }
}
