using ARGOS.SharedKernel.Results;

namespace ARGOS.Domain.Aggregates.OrganizationSetup.Errors;

public static class OrganizationSetupErrors
{
    public static readonly Error InvalidActionContext = new(
        "OrganizationSetup.InvalidActionContext",
        "Actor, policy and authorization references plus a UTC timestamp are required.");

    public static readonly Error DraftRequired = new(
        "OrganizationSetup.DraftRequired",
        "Only a draft organizational configuration can be changed.");

    public static readonly Error InvalidCatalogItem = new(
        "OrganizationSetup.InvalidCatalogItem",
        "Code and name are required and must respect their maximum lengths.");

    public static readonly Error DuplicateCode = new(
        "OrganizationSetup.DuplicateCode",
        "The code is already used in this organizational catalog.");

    public static readonly Error UnitNotFound = new(
        "OrganizationSetup.UnitNotFound",
        "The selected organizational unit does not exist in this configuration.");

    public static readonly Error UnitHasDependants = new(
        "OrganizationSetup.UnitHasDependants",
        "The unit cannot be removed while it has subordinate units or positions.");

    public static readonly Error PositionNotFound = new(
        "OrganizationSetup.PositionNotFound",
        "The selected institutional position does not exist in this configuration.");

    public static readonly Error CompetenceNotFound = new(
        "OrganizationSetup.CompetenceNotFound",
        "The selected competence does not exist in this configuration.");

    public static readonly Error ActingCapacityNotFound = new(
        "OrganizationSetup.ActingCapacityNotFound",
        "The selected acting capacity does not exist in this configuration.");

    public static readonly Error IncompleteConfiguration = new(
        "OrganizationSetup.IncompleteConfiguration",
        "Every term and required organizational catalog must be configured before publication.");

    public static readonly Error PositionWithoutCompetence = new(
        "OrganizationSetup.PositionWithoutCompetence",
        "Every institutional position must have at least one configured competence.");

    public static readonly Error VersionAlreadyHasSuccessor = new(
        "OrganizationSetup.VersionAlreadyHasSuccessor",
        "This organizational configuration already has a later draft.");

    public static readonly Error PublishedVersionRequired = new(
        "OrganizationSetup.PublishedVersionRequired",
        "Only the latest published configuration can create a new version.");

    public static readonly Error InvalidPublishedPredecessor = new(
        "OrganizationSetup.InvalidPublishedPredecessor",
        "The configuration selected for retirement is not the published version of this organization.");
}
