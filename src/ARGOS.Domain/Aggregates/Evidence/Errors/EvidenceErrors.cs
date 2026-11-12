using ARGOS.SharedKernel.Results;

namespace ARGOS.Domain.Aggregates.Evidence.Errors;

public static class EvidenceErrors
{
    public static readonly Error InvalidEvidenceData = new(
        "Evidence.InvalidData",
        "Custodian, evidence type and subject information are required.");

    public static readonly Error InvalidSourceData = new(
        "Evidence.InvalidSourceData",
        "Source type, reference, role and reception channel are required.");

    public static readonly Error InvalidSourceDates = new(
        "Evidence.InvalidSourceDates",
        "Evidence cannot be received before it was produced.");

    public static readonly Error DuplicateSource = new(
        "Evidence.DuplicateSource",
        "The same source cannot be added to an evidence more than once.");

    public static readonly Error InactiveEvidenceCannotChange = new(
        "Evidence.InactiveCannotChange",
        "Sources cannot be added to evidence that is no longer active.");
}
