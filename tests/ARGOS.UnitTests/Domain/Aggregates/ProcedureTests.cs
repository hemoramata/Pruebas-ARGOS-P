using ARGOS.Domain.Aggregates.Procedure;
using ARGOS.Domain.Aggregates.Procedure.Enums;
using ARGOS.Domain.Aggregates.Procedure.Events;

namespace ARGOS.UnitTests.Domain.Aggregates;

public sealed class ProcedureTests
{
    [Fact]
    public void Create_NormalizesDataAndStartsAsDraft()
    {
        var procedure = CreateProcedure();

        Assert.Equal("P-001", procedure.Code);
        Assert.Equal("Solicitud de constancia", procedure.Name);
        Assert.Equal("Emisión de una constancia", procedure.Description);
        Assert.Equal(ProcedureStatus.Draft, procedure.Status);
        Assert.Equal(1, procedure.Version);
        Assert.Null(procedure.PreviousVersionId);
        Assert.Null(procedure.NextVersionId);
        Assert.Empty(procedure.Decisions);
        Assert.Single(procedure.DomainEvents);
    }

    [Fact]
    public void Create_RejectsAnEmptyCode()
    {
        Assert.Throws<ArgumentException>(() =>
            Procedure.Create(" ", "Nombre", "Descripción"));
    }

    [Fact]
    public void ApprovalLifecycle_RequiresEveryConfiguredApprovalBeforePublishing()
    {
        var procedure = CreateProcedure();

        Assert.True(procedure.SubmitForReview(
            requiredApprovalCount: 2,
            separationOfDutiesRequired: true,
            emergencyPublicationAllowed: false,
            emergencyRatificationRequired: false,
            Context("redactor")).IsSuccess);
        Assert.Equal(ProcedureStatus.InReview, procedure.Status);

        Assert.True(procedure.Approve(Context("aprobador-1")).IsSuccess);
        Assert.Equal(ProcedureStatus.InReview, procedure.Status);

        Assert.True(procedure.Approve(Context("aprobador-2")).IsSuccess);
        Assert.Equal(ProcedureStatus.Approved, procedure.Status);

        Assert.True(procedure.Publish(null, Context("publicador")).IsSuccess);
        Assert.Equal(ProcedureStatus.Published, procedure.Status);
        Assert.Equal(4, procedure.Decisions.Count);
    }

    [Fact]
    public void Approve_RejectsTheSubmitterWhenSeparationOfDutiesIsRequired()
    {
        var procedure = CreateProcedure();
        Submit(procedure, "misma-persona", separationOfDutiesRequired: true);

        var result = procedure.Approve(Context("misma-persona"));

        Assert.True(result.IsFailure);
        Assert.Equal("Procedure.SeparationOfDutiesRequired", result.Error.Code);
        Assert.Equal(ProcedureStatus.InReview, procedure.Status);
        Assert.Single(procedure.Decisions);
    }

    [Fact]
    public void Approve_RejectsDuplicateApprovalFromTheSameActor()
    {
        var procedure = CreateProcedure();
        Assert.True(procedure.SubmitForReview(
            requiredApprovalCount: 2,
            separationOfDutiesRequired: true,
            emergencyPublicationAllowed: false,
            emergencyRatificationRequired: false,
            Context("redactor")).IsSuccess);
        Assert.True(procedure.Approve(Context("aprobador")).IsSuccess);

        var result = procedure.Approve(Context("APROBADOR"));

        Assert.True(result.IsFailure);
        Assert.Equal("Procedure.DuplicateApproval", result.Error.Code);
        Assert.Equal(ProcedureStatus.InReview, procedure.Status);
    }

    [Fact]
    public void Reject_PreservesDecisionAndAllowsANewSequentialRevision()
    {
        var version1 = CreateAndPublish();
        var version2Result = version1.CreateNewVersion("Versión 2", "Revisión 2");
        Assert.True(version2Result.IsSuccess);
        var version2 = version2Result.Value;
        Submit(version2, "redactor-v2");

        Assert.True(version2.Reject(Context("revisor")).IsSuccess);
        var version3Result = version2.CreateNewVersion("Versión 3", "Revisión corregida");

        Assert.True(version3Result.IsSuccess);
        Assert.Equal(ProcedureStatus.Rejected, version2.Status);
        Assert.Equal(ProcedureStatus.Draft, version3Result.Value.Status);
        Assert.Equal(3, version3Result.Value.Version);
        Assert.Equal(version2.Id, version3Result.Value.PreviousVersionId);
        Assert.Contains(
            version2.Decisions,
            decision => decision.Type == ProcedureDecisionType.Rejected);
    }

    [Fact]
    public void CreateNewVersion_KeepsPublishedVersionEffectiveUntilReplacementPublication()
    {
        var original = CreateAndPublish();
        original.ClearDomainEvents();

        var result = original.CreateNewVersion(
            "Nombre actualizado",
            "Descripción actualizada");

        Assert.True(result.IsSuccess);
        var revision = result.Value;
        Assert.Equal(ProcedureStatus.Published, original.Status);
        Assert.Equal(ProcedureStatus.Draft, revision.Status);
        Assert.Equal(original.Id, revision.PreviousVersionId);
        Assert.Equal(revision.Id, original.NextVersionId);
        Assert.Contains(
            revision.DomainEvents,
            domainEvent => domainEvent is ProcedureVersionCreatedEvent);

        Submit(revision, "redactor-v2");
        Assert.True(revision.Approve(Context("aprobador-v2")).IsSuccess);
        Assert.True(revision.Publish(original, Context("publicador-v2")).IsSuccess);

        Assert.Equal(ProcedureStatus.Retired, original.Status);
        Assert.Equal(ProcedureStatus.Published, revision.Status);
        Assert.Contains(
            revision.DomainEvents,
            domainEvent => domainEvent is ProcedureVersionPublishedEvent);
    }

    [Fact]
    public void CreateNewVersion_CannotBranchFromAVersionWithASuccessor()
    {
        var original = CreateAndPublish();
        Assert.True(original.CreateNewVersion("Versión 2", "Descripción 2").IsSuccess);

        var result = original.CreateNewVersion("Otra versión 2", "Otra descripción");

        Assert.True(result.IsFailure);
        Assert.Equal("Procedure.VersionAlreadyHasSuccessor", result.Error.Code);
    }

    [Fact]
    public void CreateNewVersion_CannotStartFromADraft()
    {
        var procedure = CreateProcedure();

        var result = procedure.CreateNewVersion("Versión 2", "Descripción 2");

        Assert.True(result.IsFailure);
        Assert.Equal("Procedure.VersionCannotBeRevised", result.Error.Code);
    }

    [Fact]
    public void Publish_RejectsAnUnapprovedVersionWithoutChangingPublishedPredecessor()
    {
        var original = CreateAndPublish();
        var revision = original.CreateNewVersion("Versión 2", "Descripción 2").Value;

        var result = revision.Publish(original, Context("publicador"));

        Assert.True(result.IsFailure);
        Assert.Equal(ProcedureStatus.Draft, revision.Status);
        Assert.Equal(ProcedureStatus.Published, original.Status);
    }

    [Fact]
    public void EmergencyPublication_RequiresAndRecordsLaterRatification()
    {
        var procedure = CreateProcedure();
        Assert.True(procedure.SubmitForReview(
            requiredApprovalCount: 2,
            separationOfDutiesRequired: true,
            emergencyPublicationAllowed: true,
            emergencyRatificationRequired: true,
            Context("redactor-emergencia")).IsSuccess);

        var dueAtUtc = DateTime.UtcNow.AddHours(4);
        Assert.True(procedure.PublishEmergency(
            currentlyPublished: null,
            Context("publicador-emergencia"),
            dueAtUtc).IsSuccess);

        Assert.Equal(ProcedureStatus.Published, procedure.Status);
        Assert.True(procedure.EmergencyRatificationPending);
        Assert.Equal(dueAtUtc, procedure.EmergencyRatificationDueAtUtc);

        Assert.True(procedure.RatifyEmergencyPublication(
            Context("ratificador-emergencia")).IsSuccess);
        Assert.False(procedure.EmergencyRatificationPending);
        Assert.Contains(
            procedure.Decisions,
            decision => decision.Type == ProcedureDecisionType.EmergencyRatified);
    }

    [Fact]
    public void EmergencyPublication_CannotBeEnabledWithoutRatification()
    {
        var procedure = CreateProcedure();

        var result = procedure.SubmitForReview(
            requiredApprovalCount: 1,
            separationOfDutiesRequired: true,
            emergencyPublicationAllowed: true,
            emergencyRatificationRequired: false,
            Context("redactor"));

        Assert.True(result.IsFailure);
        Assert.Equal("Procedure.InvalidApprovalPolicy", result.Error.Code);
        Assert.Equal(ProcedureStatus.Draft, procedure.Status);
    }

    [Fact]
    public void InvalidDecisionContext_IsRejected()
    {
        var result = ProcedureDecisionContext.Create(
            actorReference: " ",
            assignmentReference: "assignment",
            positionReference: "position",
            competenceCode: "approve",
            policyVersion: "policy-v1",
            authorizationReference: "authorization",
            comments: null,
            occurredAtUtc: DateTime.UtcNow);

        Assert.True(result.IsFailure);
        Assert.Equal("Procedure.InvalidDecisionContext", result.Error.Code);
    }

    private static Procedure CreateProcedure()
        => Procedure.Create(
            "  p-001  ",
            "  Solicitud de constancia  ",
            "  Emisión de una constancia  ");

    private static Procedure CreateAndPublish()
    {
        var procedure = CreateProcedure();
        Submit(procedure, "redactor");
        Assert.True(procedure.Approve(Context("aprobador")).IsSuccess);
        Assert.True(procedure.Publish(null, Context("publicador")).IsSuccess);
        return procedure;
    }

    private static void Submit(
        Procedure procedure,
        string actor,
        bool separationOfDutiesRequired = true)
    {
        Assert.True(procedure.SubmitForReview(
            requiredApprovalCount: 1,
            separationOfDutiesRequired,
            emergencyPublicationAllowed: false,
            emergencyRatificationRequired: false,
            Context(actor)).IsSuccess);
    }

    private static ProcedureDecisionContext Context(string actor)
        => ProcedureDecisionContext.Create(
            actor,
            $"assignment-{actor}",
            $"position-{actor}",
            "PROCEDURE-APPROVAL",
            "policy-v1",
            $"authorization-{actor}",
            "Test decision",
            DateTime.UtcNow).Value;
}
