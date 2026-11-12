namespace ARGOS.Domain.Aggregates.CaseFile.Enums;

/// <summary>
/// Representa el estado actual del expediente.
/// </summary>
public enum CaseFileStatus
{
    /// <summary>
    /// The case file has been registered but its route has not started.
    /// </summary>
    Created = 0,

    /// <summary>
    /// The case file is executing its route through institutional events.
    /// </summary>
    InProgress = 1,

    /// <summary>
    /// The case file cannot continue because it depends on a pending action.
    /// </summary>
    Waiting = 2,

    /// <summary>
    /// The procedure concluded with a definitive result.
    /// </summary>
    Finalized = 3,

    /// <summary>
    /// The case file left the operational cycle and is retained historically.
    /// </summary>
    Archived = 4
}
