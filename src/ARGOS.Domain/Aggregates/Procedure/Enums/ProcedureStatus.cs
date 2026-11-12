namespace ARGOS.Domain.Aggregates.Procedure.Enums;

/// <summary>
/// Represents the lifecycle status of a procedure.
/// </summary>
public enum ProcedureStatus
{
    /// <summary>
    /// Version is being prepared and is not operational.
    /// </summary>
    Draft = 1,

    /// <summary>
    /// Version is awaiting one or more approval decisions.
    /// </summary>
    InReview = 2,

    /// <summary>
    /// Version has received every required approval but is not yet operational.
    /// </summary>
    Approved = 3,

    /// <summary>
    /// Version is effective and available for new processes.
    /// </summary>
    Published = 4,

    /// <summary>
    /// Version was rejected and remains available as immutable history.
    /// </summary>
    Rejected = 5,

    /// <summary>
    /// Version is no longer selected for new processes.
    /// </summary>
    Retired = 6,

    /// <summary>
    /// Version is retained only for long-term historical purposes.
    /// </summary>
    Archived = 7
}
