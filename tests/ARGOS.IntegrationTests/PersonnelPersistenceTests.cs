using ARGOS.Domain.Aggregates.Organization.ValueObjects;
using ARGOS.Domain.Aggregates.Personnel;
using ARGOS.Infrastructure.Persistence;
using Npgsql;
using Microsoft.EntityFrameworkCore;

namespace ARGOS.IntegrationTests;

public sealed class PersonnelPersistenceTests
{
    [Fact]
    public async Task AssignmentAndRevocationSurviveReload()
    {
        await using var connection = new TestPostgresDatabase();
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<PersonnelDbContext>().UseNpgsql(connection.ConnectionString).Options;
        var organization = OrganizationId.New();
        var context = ARGOS.Domain.Aggregates.OrganizationSetup.OrganizationSetupActionContext.Create(
            "test", "v1", "test-authorization", DateTime.UtcNow).Value;
        var setup = ARGOS.Domain.Aggregates.OrganizationSetup.OrganizationSetup.CreateInitial(organization, context).Value;
        foreach (var concept in Enum.GetValues<ARGOS.Domain.Aggregates.OrganizationSetup.Enums.OrganizationConcept>())
            Assert.True(setup.SetTerm(concept, concept.ToString(), concept + " plural").IsSuccess);
        var unit = setup.AddUnit("u", "Unidad").Value;
        var position = setup.AddPosition("p", "Puesto", unit).Value;
        var competence = setup.AddCompetence("c", "Competencia", "Descripción").Value;
        var capacity = setup.AddActingCapacity("a", "Calidad").Value;
        Assert.True(setup.AssignCompetence(position, competence).IsSuccess);
        Assert.True(setup.AssignActingCapacity(position, capacity).IsSuccess);
        Assert.True(setup.Publish(null, context).IsSuccess);
        var person = new InstitutionalPerson(organization, new OfficialIdentity("Emisor", "Documento", "002"), "Prueba");
        var start = DateTimeOffset.UtcNow;
        var assignment = person.Assign(setup, position, start, null, "assignment-authorization");
        Assert.Throws<ArgumentException>(() => AdministratorDesignation.Create(person, Guid.NewGuid(),
            "agreement", "Institution", "registrar", start, start, null));
        Assert.Throws<ArgumentException>(() => AdministratorDesignation.Create(person, assignment.Id,
            "", "Institution", "registrar", start, start, null));
        Assert.Throws<ArgumentException>(() => AdministratorDesignation.Create(person, assignment.Id,
            "agreement", "Institution", "registrar", start, start.AddDays(-1), null));
        var designation = AdministratorDesignation.Create(person, assignment.Id,
            "institutional-agreement-001", "Institutional authority", "authorized-registrar", start, start, start.AddDays(30));
        Assert.False(designation.IsEffectiveAt(start.AddSeconds(-1)));
        Assert.True(designation.IsEffectiveAt(start));
        Assert.False(designation.IsEffectiveAt(start.AddDays(30)));
        var designationOptions = new DbContextOptionsBuilder<DesignationDbContext>().UseNpgsql(connection.ConnectionString).Options;
        await using (var designationDb = new DesignationDbContext(designationOptions))
        {
            await designationDb.InitializeSchemaAsync();
            await designationDb.InitializeSchemaAsync();
            designationDb.Designations.Add(designation);
            await designationDb.SaveChangesAsync();
        }
        await using (var designationDb = new DesignationDbContext(designationOptions))
        {
            var restoredDesignation = await designationDb.Designations.SingleAsync();
            Assert.Equal(assignment.Id, restoredDesignation.AssignmentId);
            Assert.Equal("institutional-agreement-001", restoredDesignation.AuthorizationReference);
            restoredDesignation.Revoke(start.AddHours(1), "institutional-revocation-002");
            Assert.Throws<InvalidOperationException>(() => restoredDesignation.Revoke(start.AddHours(2), "duplicate"));
            await designationDb.SaveChangesAsync();
        }
        await using (var designationDb = new DesignationDbContext(designationOptions))
        {
            var restoredDesignation = await designationDb.Designations.SingleAsync();
            Assert.False(restoredDesignation.IsEffectiveAt(start.AddHours(1)));
            Assert.Equal("institutional-revocation-002", restoredDesignation.RevocationReference);
        }
        await using (var first = new PersonnelDbContext(options))
        {
            await PersonnelSchema.InitializeAsync(first);
            first.People.Add(person);
            await first.SaveChangesAsync();
        }
        await using (var second = new PersonnelDbContext(options))
        {
            var restored = await second.People.SingleAsync();
            Assert.Equal(assignment.Id, Assert.Single(restored.Assignments).Id);
            Assert.Contains(competence, restored.GetEffectiveCompetenceIds(setup, start));
            restored.RevokeAssignment(assignment.Id, start.AddMinutes(1), "revocation-authorization");
            await second.SaveChangesAsync();
        }
        await using var third = new PersonnelDbContext(options);
        var final = await third.People.SingleAsync();
        Assert.Empty(final.GetEffectiveCompetenceIds(setup, start.AddMinutes(1)));
        Assert.Equal("revocation-authorization", Assert.Single(final.Assignments).RevocationAuthorizationReference);
    }

    [Fact]
    public async Task AdditiveSchemaPreservesExistingTablesAndPersonSurvivesReload()
    {
        await using var connection = new TestPostgresDatabase();
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<PersonnelDbContext>().UseNpgsql(connection.ConnectionString).Options;
        var organization = OrganizationId.New();
        var person = new InstitutionalPerson(organization, new OfficialIdentity("Emisor", "Documento", "001"), "Ana Prueba");
        await using (var first = new PersonnelDbContext(options))
        {
            await first.Database.ExecuteSqlRawAsync("CREATE TABLE ExistingData (Value TEXT); INSERT INTO ExistingData VALUES ('preserved');");
            await PersonnelSchema.InitializeAsync(first);
            await PersonnelSchema.InitializeAsync(first);
            first.People.Add(person);
            await first.SaveChangesAsync();
        }
        await using var second = new PersonnelDbContext(options);
        var restored = await second.People.SingleAsync(p => p.Id == person.Id);
        Assert.Equal("001", restored.Identity.Number);
        Assert.Equal("Ana Prueba", restored.FullName);
        Assert.Equal(organization, restored.OrganizationId);
        Assert.Equal("preserved", await second.Database.SqlQueryRaw<string>("SELECT value AS \"Value\" FROM ExistingData").SingleAsync());
        second.People.Add(new InstitutionalPerson(organization, restored.Identity, "Duplicado"));
        await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());
    }
}
