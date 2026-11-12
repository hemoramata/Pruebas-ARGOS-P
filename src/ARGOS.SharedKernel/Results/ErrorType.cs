namespace ARGOS.SharedKernel.Results;

/// <summary>
/// Categorizes domain errors so callers can decide how to translate them
/// into HTTP responses, UI messages, or logging strategies.
/// </summary>
public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    BusinessRule,
    Unauthorized,
    Unexpected
}
