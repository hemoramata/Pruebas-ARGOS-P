using ARGOS.Domain.Aggregates.Organization.ValueObjects;
using ARGOS.Domain.Aggregates.OrganizationSetup;
using ARGOS.Domain.Aggregates.OrganizationSetup.Enums;

namespace ARGOS.UnitTests.Domain.Aggregates;

public sealed class OrganizationSetupTests
{
    [Fact]
    public void CreateInitial_StartsAnEmptyAuthorizedDraft()
    {
        var organizationId = OrganizationId.New();
        var context = Context("implantador");

        var result = OrganizationSetup.CreateInitial(organizationId, context);

        Assert.True(result.IsSuccess);
        Assert.Equal(organizationId, result.Value.OrganizationId);
        Assert.Equal(1, result.Value.Version);
        Assert.Equal(OrganizationSetupStatus.Draft, result.Value.Status);
        Assert.Equal("implantador", result.Value.CreatedByReference);
        Assert.Empty(result.Value.Terms);
        Assert.Single(result.Value.DomainEvents);
    }

    [Fact]
    public void Terms_AreDefinedByTheInstitutionWithoutChangingInternalConcepts()
    {
        var setup = CreateDraft();

        Assert.True(setup.SetTerm(
            OrganizationConcept.OrganizationUnit,
            "Dirección municipal",
            "Direcciones municipales").IsSuccess);
        Assert.True(setup.SetTerm(
            OrganizationConcept.InstitutionalPosition,
            "Puesto",
            "Puestos").IsSuccess);

        Assert.Contains(
            setup.Terms,
            term => term.Concept == OrganizationConcept.OrganizationUnit
                && term.Singular == "Dirección municipal");
        Assert.Contains(
            setup.Terms,
            term => term.Concept == OrganizationConcept.InstitutionalPosition
                && term.Plural == "Puestos");
    }

    [Fact]
    public void CompleteCustomConfiguration_CanBePublished()
    {
        var configured = CreateCompleteDraft();

        var result = configured.Setup.Publish(null, Context("publicador"));

        Assert.True(result.IsSuccess);
        Assert.Equal(OrganizationSetupStatus.Published, configured.Setup.Status);
        Assert.NotNull(configured.Setup.PublishedAtUtc);
        Assert.Equal("Dirección de Gestión", Assert.Single(configured.Setup.Units).Name);
        Assert.Equal("Gestor institucional", Assert.Single(configured.Setup.Positions).Name);
        Assert.Contains(
            configured.CompetenceId,
            Assert.Single(configured.Setup.Positions).CompetenceIds);
        Assert.Contains(
            configured.CapacityId,
            Assert.Single(configured.Setup.Positions).ActingCapacityIds);
    }

    [Fact]
    public void Publish_RejectsAnIncompleteConfiguration()
    {
        var setup = CreateDraft();
        Assert.True(setup.SetTerm(
            OrganizationConcept.Organization,
            "Municipalidad",
            "Municipalidades").IsSuccess);

        var result = setup.Publish(null, Context("publicador"));

        Assert.True(result.IsFailure);
        Assert.Equal("OrganizationSetup.IncompleteConfiguration", result.Error.Code);
        Assert.Equal(OrganizationSetupStatus.Draft, setup.Status);
    }

    [Fact]
    public void Publish_RejectsAPositionWithoutCompetence()
    {
        var setup = CreateDraft();
        ConfigureEveryTerm(setup);
        var unitId = setup.AddUnit("unidad", "Unidad").Value;
        Assert.True(setup.AddCompetence("tramitar", "Tramitar").IsSuccess);
        Assert.True(setup.AddActingCapacity("titular", "Titular").IsSuccess);
        Assert.True(setup.AddPosition("puesto", "Puesto", unitId).IsSuccess);

        var result = setup.Publish(null, Context("publicador"));

        Assert.True(result.IsFailure);
        Assert.Equal("OrganizationSetup.PositionWithoutCompetence", result.Error.Code);
    }

    [Fact]
    public void CatalogCodes_AreNormalizedAndUniqueIgnoringCase()
    {
        var setup = CreateDraft();

        var first = setup.AddUnit("  dir-fin  ", "Dirección Financiera");
        var duplicate = setup.AddUnit("DIR-FIN", "Otra Dirección");

        Assert.True(first.IsSuccess);
        Assert.Equal("DIR-FIN", Assert.Single(setup.Units).Code);
        Assert.True(duplicate.IsFailure);
        Assert.Equal("OrganizationSetup.DuplicateCode", duplicate.Error.Code);
    }

    [Fact]
    public void References_MustBelongToTheSameConfiguration()
    {
        var setup = CreateDraft();

        var child = setup.AddUnit("child", "Unidad hija", Guid.NewGuid());
        var position = setup.AddPosition("position", "Puesto", Guid.NewGuid());
        var assignedCompetence = setup.AssignCompetence(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal("OrganizationSetup.UnitNotFound", child.Error.Code);
        Assert.Equal("OrganizationSetup.UnitNotFound", position.Error.Code);
        Assert.Equal("OrganizationSetup.PositionNotFound", assignedCompetence.Error.Code);
    }

    [Fact]
    public void PublishedConfiguration_IsImmutable()
    {
        var configured = CreateCompleteDraft();
        Assert.True(configured.Setup.Publish(null, Context("publicador")).IsSuccess);

        var result = configured.Setup.AddUnit("otra", "Otra unidad");

        Assert.True(result.IsFailure);
        Assert.Equal("OrganizationSetup.DraftRequired", result.Error.Code);
        Assert.Single(configured.Setup.Units);
    }

    [Fact]
    public void NewVersion_ClonesStableIdsWithoutChangingPublishedHistory()
    {
        var configured = CreateCompleteDraft();
        var original = configured.Setup;
        Assert.True(original.Publish(null, Context("publicador-v1")).IsSuccess);

        var nextResult = original.CreateNextVersion(Context("implantador-v2"));
        Assert.True(nextResult.IsSuccess);
        var next = nextResult.Value;

        Assert.Equal(OrganizationSetupStatus.Published, original.Status);
        Assert.Equal(OrganizationSetupStatus.Draft, next.Status);
        Assert.Equal(2, next.Version);
        Assert.Equal(original.Id, next.PreviousVersionId);
        Assert.Equal(next.Id, original.NextVersionId);
        Assert.Equal(configured.UnitId, Assert.Single(next.Units).Id);
        Assert.Equal(configured.PositionId, Assert.Single(next.Positions).Id);

        Assert.True(next.RenameUnit(configured.UnitId, "Gerencia Institucional").IsSuccess);
        Assert.True(next.RenamePosition(configured.PositionId, "Gestor principal").IsSuccess);
        Assert.Equal("Dirección de Gestión", Assert.Single(original.Units).Name);
        Assert.Equal("Gestor institucional", Assert.Single(original.Positions).Name);

        Assert.True(next.Publish(original, Context("publicador-v2")).IsSuccess);
        Assert.Equal(OrganizationSetupStatus.Retired, original.Status);
        Assert.Equal(OrganizationSetupStatus.Published, next.Status);
    }

    [Fact]
    public void Versioning_RejectsBranchesFromTheSamePublishedConfiguration()
    {
        var configured = CreateCompleteDraft();
        Assert.True(configured.Setup.Publish(null, Context("publicador")).IsSuccess);
        Assert.True(configured.Setup.CreateNextVersion(Context("autor-v2")).IsSuccess);

        var result = configured.Setup.CreateNextVersion(Context("otro-autor-v2"));

        Assert.True(result.IsFailure);
        Assert.Equal("OrganizationSetup.VersionAlreadyHasSuccessor", result.Error.Code);
    }

    [Fact]
    public void NextVersion_CannotPublishWithoutRetiringItsExactPredecessor()
    {
        var configured = CreateCompleteDraft();
        Assert.True(configured.Setup.Publish(null, Context("publicador-v1")).IsSuccess);
        var next = configured.Setup.CreateNextVersion(Context("autor-v2")).Value;

        var result = next.Publish(null, Context("publicador-v2"));

        Assert.True(result.IsFailure);
        Assert.Equal("OrganizationSetup.InvalidPublishedPredecessor", result.Error.Code);
        Assert.Equal(OrganizationSetupStatus.Published, configured.Setup.Status);
        Assert.Equal(OrganizationSetupStatus.Draft, next.Status);
    }

    [Fact]
    public void Publish_RejectsAConfigurationFromAnotherOrganizationAsPredecessor()
    {
        var first = CreateCompleteDraft();
        var second = CreateCompleteDraft();
        Assert.True(first.Setup.Publish(null, Context("publicador-1")).IsSuccess);

        var result = second.Setup.Publish(first.Setup, Context("publicador-2"));

        Assert.True(result.IsFailure);
        Assert.Equal("OrganizationSetup.InvalidPublishedPredecessor", result.Error.Code);
        Assert.Equal(OrganizationSetupStatus.Published, first.Setup.Status);
        Assert.Equal(OrganizationSetupStatus.Draft, second.Setup.Status);
    }

    [Fact]
    public void ActionContext_RequiresUtcAndAuthorizationReferences()
    {
        var result = OrganizationSetupActionContext.Create(
            actorReference: "",
            policyVersion: "setup-policy-v1",
            authorizationReference: "authorization",
            occurredAtUtc: DateTime.Now);

        Assert.True(result.IsFailure);
        Assert.Equal("OrganizationSetup.InvalidActionContext", result.Error.Code);
    }

    private static OrganizationSetup CreateDraft()
        => OrganizationSetup.CreateInitial(
            OrganizationId.New(),
            Context("implantador")).Value;

    private static ConfiguredSetup CreateCompleteDraft()
    {
        var setup = CreateDraft();
        ConfigureEveryTerm(setup);

        var unitId = setup.AddUnit(
            "dir-gestion",
            "Dirección de Gestión").Value;
        var competenceId = setup.AddCompetence(
            "resolver",
            "Resolver solicitudes",
            "Competencia configurada por la institución").Value;
        var capacityId = setup.AddActingCapacity(
            "titular",
            "En calidad de titular").Value;
        var positionId = setup.AddPosition(
            "gestor",
            "Gestor institucional",
            unitId).Value;

        Assert.True(setup.AssignCompetence(positionId, competenceId).IsSuccess);
        Assert.True(setup.AssignActingCapacity(positionId, capacityId).IsSuccess);

        return new ConfiguredSetup(
            setup,
            unitId,
            positionId,
            competenceId,
            capacityId);
    }

    private static void ConfigureEveryTerm(OrganizationSetup setup)
    {
        foreach (var concept in Enum.GetValues<OrganizationConcept>())
        {
            Assert.True(setup.SetTerm(
                concept,
                $"{concept} local",
                $"{concept} locales").IsSuccess);
        }
    }

    private static OrganizationSetupActionContext Context(string actor)
        => OrganizationSetupActionContext.Create(
            actor,
            "setup-policy-v1",
            $"authorization-{actor}",
            DateTime.UtcNow).Value;

    private sealed record ConfiguredSetup(
        OrganizationSetup Setup,
        Guid UnitId,
        Guid PositionId,
        Guid CompetenceId,
        Guid CapacityId);
}
