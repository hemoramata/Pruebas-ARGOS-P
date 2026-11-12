namespace ARGOS.Domain.Aggregates.OrganizationUnit.Errors;

/// <summary>
/// Domain error messages for Organization Unit.
/// </summary>
public static class OrganizationUnitErrors
{
    public const string NameIsRequired =
        "Organization unit name is required.";

    public const string CodeIsRequired =
        "Organization unit code is required.";

    public const string NameTooLong =
        "Organization unit name cannot exceed 200 characters.";

    public const string CodeTooLong =
        "Organization unit code cannot exceed 50 characters.";

    public const string AlreadyActive =
        "Organization unit is already active.";

    public const string AlreadyInactive =
        "Organization unit is already inactive.";
}