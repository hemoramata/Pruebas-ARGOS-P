using ARGOS.Application.Security;
using ARGOS.Domain.Aggregates.Organization.ValueObjects;
using ARGOS.Domain.Aggregates.Personnel;
using ARGOS.Infrastructure;
using ARGOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ARGOS.IntegrationTests;

public sealed class PasswordRecoveryTests
{
    [Fact]
    public async Task ResetIsAtomicAuditedBoundToCredentialAndPreservesIdentity()
    {
        await using var database=new TestPostgresDatabase(); await database.OpenAsync();
        await TestPostgresDatabase.InitializeAsync(database.ConnectionString);
        var services=new ServiceCollection().AddInfrastructure(database.ConnectionString).AddSingleton<TimeProvider>(TimeProvider.System);
        await using var provider=services.BuildServiceProvider();
        var person=new InstitutionalPerson(OrganizationId.From(Guid.NewGuid()),new OfficialIdentity("CR","ID","001"),"Recovery test");
        await using(var scope=provider.CreateAsyncScope())
        {
            var personnel=scope.ServiceProvider.GetRequiredService<PersonnelDbContext>(); personnel.People.Add(person); await personnel.SaveChangesAsync();
            var db=scope.ServiceProvider.GetRequiredService<AccessDbContext>();
            var account=AccessAccount.Provision(person.Id,"old-alias","Old-password-1",VerificationChannel.InstitutionalEmail,"test@example.com","email-challenge:verified");
            account.LockedUntilTicks=DateTimeOffset.UtcNow.AddMinutes(15).UtcTicks; account.FailedAttempts=3;
            db.Accounts.Add(account); await db.SaveChangesAsync();
        }
        var recovery=provider.GetRequiredService<IPasswordRecoveryAccounts>();
        Assert.Null(await recovery.FindAsync("old-alias",default));
        var target=(await recovery.FindAsync("001",default))!;
        Assert.NotNull(target);
        await Assert.ThrowsAsync<ArgumentException>(()=>recovery.ResetAsync(target,"weak",Guid.NewGuid(),default));
        Assert.True(await recovery.ResetAsync(target,"New-password-2",Guid.NewGuid(),default));
        Assert.False(await recovery.ResetAsync(target,"Another-password-3",Guid.NewGuid(),default));
        await using(var scope=provider.CreateAsyncScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<AccessDbContext>(); var account=await db.Accounts.SingleAsync();
            Assert.True(account.VerifyPassword("New-password-2")); Assert.False(account.VerifyPassword("Old-password-1"));
            Assert.Equal(0,account.FailedAttempts); Assert.Equal(0,account.LockedUntilTicks); Assert.False(account.MustChangePassword);
            Assert.Equal(person.Id,account.PersonId); Assert.Equal("test@example.com",account.Destination);
            var audit=await db.SensitiveDecisions.SingleAsync(); Assert.Equal("account.password.recover",audit.OperationCode);
            Assert.Equal(person.Id,audit.AuthorizedBy); Assert.Equal(SensitiveDecisionStatus.Executed,audit.Status);
            var pending=(await recovery.FindAsync("001",default))!;
            account.Disable(); await db.SaveChangesAsync();
            Assert.False(await recovery.ResetAsync(pending,"Disabled-password-4",Guid.NewGuid(),default));
            Assert.Null(await recovery.FindAsync("001",default));
        }
    }
}
