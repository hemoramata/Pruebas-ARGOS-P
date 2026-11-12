using ARGOS.Domain.Aggregates.Organization.ValueObjects;
using ARGOS.Domain.Aggregates.OrganizationSetup;
using ARGOS.Domain.Aggregates.OrganizationSetup.Enums;
using ARGOS.Domain.Aggregates.Personnel;

namespace ARGOS.UnitTests.Domain.Aggregates;

public sealed class InstitutionalPersonTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IdentityPreservesLeadingZerosAndGeneratesIndependentPersonIds()
    {
        var identity = new OfficialIdentity("Emisor", "Documento", " 00123 ");
        var organization = OrganizationId.New();
        var first = new InstitutionalPerson(organization, identity, " Ana Pérez ");
        var second = new InstitutionalPerson(organization, identity, "Ana Pérez");
        Assert.Equal("00123", identity.Number);
        Assert.Equal("Ana Pérez", first.FullName);
        Assert.NotEqual(first.Id, second.Id);
        Assert.DoesNotContain("00123", identity.ToString());
    }

    [Fact]
    public void CompetencesFollowAssignmentTimeBoundaries()
    {
        var (person, setup, position, competence) = Fixture();
        person.Assign(setup, position, Start, Start.AddHours(1), "authorized");
        Assert.Empty(person.GetEffectiveCompetenceIds(setup, Start.AddTicks(-1)));
        Assert.Contains(competence, person.GetEffectiveCompetenceIds(setup, Start));
        Assert.Empty(person.GetEffectiveCompetenceIds(setup, Start.AddHours(1)));
    }

    [Fact]
    public void RevocationEndsCompetencesAndPreservesHistory()
    {
        var (person, setup, position, competence) = Fixture();
        var assignment = person.Assign(setup, position, Start, null, "authorized");
        person.RevokeAssignment(assignment.Id, Start.AddMinutes(1), "revocation-authorized");
        Assert.Contains(competence, person.GetEffectiveCompetenceIds(setup, Start));
        Assert.Empty(person.GetEffectiveCompetenceIds(setup, Start.AddMinutes(1)));
        Assert.Single(person.Assignments);
        Assert.Equal("revocation-authorized", assignment.RevocationAuthorizationReference);
    }

    [Fact]
    public void SamePositionCannotOverlapButAdjacentAssignmentsAreAllowed()
    {
        var (person, setup, position, _) = Fixture();
        person.Assign(setup, position, Start, Start.AddHours(1), "authorized");
        Assert.Throws<InvalidOperationException>(() => person.Assign(setup, position, Start.AddMinutes(30), null, "authorized"));
        person.Assign(setup, position, Start.AddHours(1), null, "authorized");
        Assert.Equal(2, person.Assignments.Count);
    }

    [Fact]
    public void ForeignOrganizationAndUnknownPositionsAreRejected()
    {
        var (person, setup, _, _) = Fixture();
        var (_, foreign, position, _) = Fixture();
        Assert.Throws<InvalidOperationException>(() => person.Assign(foreign, position, Start, null, "authorized"));
        Assert.Throws<ArgumentException>(() => person.Assign(setup, Guid.NewGuid(), Start, null, "authorized"));
    }

    [Fact]
    public void DraftAndRetiredConfigurationsAreRejected()
    {
        var (person, setup, position, _) = Fixture();
        var next = setup.CreateNextVersion(Context()).Value;
        Assert.Throws<InvalidOperationException>(() => person.Assign(next, position, Start, null, "authorized"));
        Assert.True(next.Publish(setup, Context()).IsSuccess);
        Assert.Throws<InvalidOperationException>(() => person.GetEffectiveCompetenceIds(setup, Start));
    }

    [Fact]
    public void CompetencesAreResolvedFromNewPublishedVersion()
    {
        var (person, setup, position, _) = Fixture();
        person.Assign(setup, position, Start, null, "authorized");
        var next = setup.CreateNextVersion(Context()).Value;
        var additional = next.AddCompetence("extra", "Nueva competencia", "Configurada").Value;
        Assert.True(next.AssignCompetence(position, additional).IsSuccess);
        Assert.True(next.Publish(setup, Context()).IsSuccess);
        Assert.Contains(additional, person.GetEffectiveCompetenceIds(next, Start));
    }

    [Fact]
    public void InvalidIntervalsAndMissingAuthorizationAreRejected()
    {
        var (person, setup, position, _) = Fixture();
        Assert.Throws<ArgumentException>(() => person.Assign(setup, position, Start, Start, "authorized"));
        Assert.Throws<ArgumentException>(() => person.Assign(setup, position, Start, null, " "));
        Assert.Empty(person.Assignments);
    }

    private static (InstitutionalPerson, OrganizationSetup, Guid, Guid) Fixture()
    {
        var organization = OrganizationId.New();
        var setup = OrganizationSetup.CreateInitial(organization, Context()).Value;
        foreach (var concept in Enum.GetValues<OrganizationConcept>())
            Assert.True(setup.SetTerm(concept, concept.ToString(), concept + " plural").IsSuccess);
        var unit = setup.AddUnit("unidad", "Unidad local").Value;
        var position = setup.AddPosition("puesto", "Puesto local", unit).Value;
        var competence = setup.AddCompetence("atender", "Atender", "Configurada").Value;
        var capacity = setup.AddActingCapacity("titular", "Titular").Value;
        Assert.True(setup.AssignCompetence(position, competence).IsSuccess);
        Assert.True(setup.AssignActingCapacity(position, capacity).IsSuccess);
        Assert.True(setup.Publish(null, Context()).IsSuccess);
        return (new InstitutionalPerson(organization, new OfficialIdentity("Emisor", "Documento", "00123"),
            "Persona de prueba"), setup, position, competence);
    }

    private static OrganizationSetupActionContext Context() =>
        OrganizationSetupActionContext.Create("prueba", "v1", "autorizacion-prueba", Start.UtcDateTime).Value;
}
