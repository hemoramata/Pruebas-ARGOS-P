using ARGOS.API;
using ARGOS.Application.Common.Persistence;
using ARGOS.Application.Security;
using ARGOS.Domain.Aggregates.Organization.ValueObjects;
using ARGOS.Domain.Aggregates.OrganizationSetup;
using ARGOS.Domain.Aggregates.OrganizationSetup.Enums;
using ARGOS.Domain.Aggregates.Personnel;
using ARGOS.Infrastructure;
using ARGOS.Infrastructure.Persistence;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ARGOS.IntegrationTests;

public sealed class AdministratorEnrollmentTests
{
    [Fact]
    public async Task EnrollmentIsAtomicAndRevocationRemovesAdministrativePermission()
    {
        var connectionString = TestPostgresDatabase.CreateConnectionString();
        await TestPostgresDatabase.InitializeAsync(connectionString);
        var services = new ServiceCollection();
        services.AddInfrastructure(connectionString);
        services.AddSingleton(TimeProvider.System);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ArgosDbContext>();
        await db.Database.MigrateAsync();
        await PersonnelSchema.InitializeAsync(scope.ServiceProvider.GetRequiredService<PersonnelDbContext>());
        await scope.ServiceProvider.GetRequiredService<AccessDbContext>().InitializeSchemaAsync();
        await scope.ServiceProvider.GetRequiredService<DesignationDbContext>().InitializeSchemaAsync();
        var owner = ARGOS.Domain.Aggregates.Organization.Organization.Create("Institución", "INST", null);
        var organization = owner.Id;
        var action = OrganizationSetupActionContext.Create("institution", "policy", "agreement", DateTime.UtcNow).Value;
        var setup = OrganizationSetup.CreateInitial(organization, action).Value;
        foreach (var concept in Enum.GetValues<OrganizationConcept>())
            setup.SetTerm(concept, concept.ToString(), concept + "s");
        var unit = setup.AddUnit("TI", "Tecnología").Value;
        var position = setup.AddPosition("ADM", "Administrador", unit).Value;
        setup.AssignCompetence(position, setup.AddCompetence("C", "Administración", "Gestión").Value);
        setup.AssignActingCapacity(position, setup.AddActingCapacity("T", "Titular").Value);
        Assert.True(setup.Publish(null, action).IsSuccess);
        db.Organizations.Add(owner);
        db.OrganizationSetups.Add(setup);
        await db.SaveChangesAsync();
        var now = DateTimeOffset.UtcNow;
        var person = new InstitutionalPerson(organization, new OfficialIdentity("CR", "ID", "001"), "Persona de prueba");
        var assignment = person.Assign(setup, position, now.AddDays(-1), null, "agreement");
        var designation = AdministratorDesignation.Create(person, assignment.Id, "agreement", "institution", "registrar", now, now.AddDays(-1), null);
        var account = AccessAccount.Provision(person.Id, "admin-test", "test-password-123", VerificationChannel.InstitutionalEmail,
            "test@example.com", "pending");
        await Assert.ThrowsAsync<InvalidOperationException>(() => AdministratorEnrollment.SaveAsync(connectionString, person, designation, account));
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<PersonnelDbContext>().People.CountAsync());
        account.ConfirmEnrollmentEmail($"email-challenge:{Guid.NewGuid()}");
        await AdministratorEnrollment.SaveAsync(connectionString, person, designation, account);
        var login = provider.GetRequiredService<ILoginAccounts>();
        Assert.NotNull(await login.ValidatePasswordAsync("001", "test-password-123", default));
        Assert.Null(await login.ValidatePasswordAsync("admin-test", "test-password-123", default));
        Assert.Null(await login.ValidatePasswordAsync("999", "test-password-123", default));
        Assert.Contains("personnel.manage", (await login.GetActiveAsync(person.Id, default))!.Permissions);
        // Duplicate login must roll back the person and designation too.
        var duplicate = new InstitutionalPerson(organization, new OfficialIdentity("CR", "ID", "002"), "Otra persona");
        var duplicateAssignment = duplicate.Assign(setup, position, now, null, "agreement");
        var duplicateDesignation = AdministratorDesignation.Create(duplicate, duplicateAssignment.Id, "agreement", "institution", "registrar", now, now, null);
        var duplicateAccount = AccessAccount.Provision(duplicate.Id, "admin-test", "test-password-123", VerificationChannel.InstitutionalEmail, "test@example.com", "email-challenge:test");
        await Assert.ThrowsAsync<DbUpdateException>(() => AdministratorEnrollment.SaveAsync(connectionString, duplicate, duplicateDesignation, duplicateAccount));
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<PersonnelDbContext>().People.CountAsync());
        // Same number from another document issuer must not select an arbitrary account.
        var ambiguous = new InstitutionalPerson(organization, new OfficialIdentity("OTHER", "ID", "001"), "Ambiguous identity");
        var peopleDb = scope.ServiceProvider.GetRequiredService<PersonnelDbContext>();
        peopleDb.People.Add(ambiguous);
        await peopleDb.SaveChangesAsync();
        var accountsDb = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
        accountsDb.Accounts.Add(AccessAccount.Provision(ambiguous.Id,"other-alias","another-test-password",VerificationChannel.InstitutionalEmail,"other@example.com","email-challenge:test"));
        await accountsDb.SaveChangesAsync();
        Assert.Null(await login.ValidatePasswordAsync("001","test-password-123",default));
        var designationDb = scope.ServiceProvider.GetRequiredService<DesignationDbContext>();
        (await designationDb.Designations.SingleAsync()).Revoke(DateTimeOffset.UtcNow, "revocation");
        await designationDb.SaveChangesAsync();
        Assert.DoesNotContain("personnel.manage", (await login.GetActiveAsync(person.Id, default))!.Permissions);
    }
}
