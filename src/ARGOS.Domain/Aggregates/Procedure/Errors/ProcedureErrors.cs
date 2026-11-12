using ARGOS.SharedKernel.Results;

namespace ARGOS.Domain.Aggregates.Procedure.Errors;

/// <summary>
/// Domain errors for Procedure.
/// </summary>
public static class ProcedureErrors
{
    public static readonly Error InvalidVersionData = new(
        "Procedure.InvalidVersionData",
        "The name and description of a procedure version are required.");

    public static readonly Error VersionAlreadyHasSuccessor = new(
        "Procedure.VersionAlreadyHasSuccessor",
        "A procedure version that already has a successor cannot create another version.");

    public static readonly Error ArchivedVersionCannotBeRevised = new(
        "Procedure.ArchivedVersionCannotBeRevised",
        "An archived procedure version cannot create a new version.");

    public static readonly Error VersionCannotBeRevised = new(
        "Procedure.VersionCannotBeRevised",
        "Only a published or rejected tail version can create a new version.");

    public static readonly Error InvalidTransition = new(
        "Procedure.InvalidTransition",
        "The requested procedure lifecycle transition is not valid from its current status.");

    public static readonly Error InvalidApprovalPolicy = new(
        "Procedure.InvalidApprovalPolicy",
        "The approval policy must identify its version and require between one and ten approvals.");

    public static readonly Error InvalidDecisionContext = new(
        "Procedure.InvalidDecisionContext",
        "The actor, assignment, position, competence, policy and authorization references are required.");

    public static readonly Error SeparationOfDutiesRequired = new(
        "Procedure.SeparationOfDutiesRequired",
        "The person who submitted this version cannot approve it under the selected policy.");

    public static readonly Error DuplicateApproval = new(
        "Procedure.DuplicateApproval",
        "The same actor cannot approve a procedure version more than once.");

    public static readonly Error InvalidPublishedPredecessor = new(
        "Procedure.InvalidPublishedPredecessor",
        "The version selected for retirement is not the published version of this procedure.");

    public static readonly Error EmergencyPublicationNotAllowed = new(
        "Procedure.EmergencyPublicationNotAllowed",
        "The approval policy does not allow emergency publication for this version.");

    public static readonly Error InvalidRatificationDeadline = new(
        "Procedure.InvalidRatificationDeadline",
        "Emergency ratification must have a future UTC deadline.");

    public static readonly Error EmergencyRatificationNotPending = new(
        "Procedure.EmergencyRatificationNotPending",
        "This version does not have a pending emergency ratification.");
}
