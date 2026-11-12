namespace ARGOS.Domain.Aggregates.Procedure.Enums;

/// <summary>
/// Identifies a traceable decision made during the lifecycle of a procedure version.
/// </summary>
public enum ProcedureDecisionType
{
    SubmittedForReview = 1,
    Approved = 2,
    Rejected = 3,
    Published = 4,
    Retired = 5,
    Archived = 6,
    EmergencyPublished = 7,
    EmergencyRatified = 8
}
