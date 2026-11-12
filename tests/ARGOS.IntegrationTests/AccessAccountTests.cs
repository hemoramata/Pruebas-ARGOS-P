using ARGOS.Application.Security;
using ARGOS.Infrastructure.Persistence;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ARGOS.IntegrationTests;

public sealed class AccessAccountTests
{
    [Fact]
    public void PasswordChangeRejectsTheCurrentPassword()
    {
        var account = AccessAccount.Provision(Guid.NewGuid(), "001", "Old-password-1", VerificationChannel.Sms,
            "+50688881234", "test-proof");
        var error = Assert.Throws<ArgumentException>(() => account.ChangePassword("Old-password-1"));
        Assert.Contains("diferente", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(account.VerifyPassword("Old-password-1"));
    }

    [Fact]
    public async Task ConcurrentPasswordChangesCannotBothUseTheOldPassword()
    {
        await using var connection = new TestPostgresDatabase();
        await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddDbContext<AccessDbContext>(options => options.UseNpgsql(connection.ConnectionString));
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<IPasswordChanger, PostgresLoginAccounts>();
        await using var provider = services.BuildServiceProvider();
        var id = Guid.NewGuid();
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
            await db.InitializeSchemaAsync();
            db.Accounts.Add(AccessAccount.Provision(id, "001", "old-test-password", VerificationChannel.Sms,
                "+50688881234", "test-proof"));
            await db.SaveChangesAsync();
        }
        var changer = provider.GetRequiredService<IPasswordChanger>();
        var candidates = new[] { "New-test-password-1", "New-test-password-2" };
        var results = await Task.WhenAll(candidates.Select(password =>
            changer.ChangePasswordAsync(id, "old-test-password", password, default)));
        Assert.Single(results, accepted => accepted);
        await using var check = provider.CreateAsyncScope();
        var account = await check.ServiceProvider.GetRequiredService<AccessDbContext>().Accounts.SingleAsync();
        Assert.True(account.VerifyPassword(candidates[Array.FindIndex(results, accepted => accepted)]));
        Assert.False(account.VerifyPassword("old-test-password"));
    }

    [Fact]
    public async Task CredentialsPersistWithUniqueSaltAndWithoutPlaintext()
    {
        const string password = "test-password-only-123";
        var first = AccessAccount.Provision(Guid.NewGuid(), "001", password, VerificationChannel.Sms, "+50688881234", "test-proof");
        var other = AccessAccount.Provision(Guid.NewGuid(), "002", password, VerificationChannel.Sms, "+50688881234", "test-proof");
        Assert.NotEqual(first.Salt, other.Salt);
        Assert.NotEqual(first.PasswordDigest, other.PasswordDigest);
        Assert.True(first.VerifyPassword(password));
        Assert.False(first.VerifyPassword("wrong"));
        Assert.Equal("***1234", first.MaskedDestination);
        await using var connection = new TestPostgresDatabase();
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AccessDbContext>().UseNpgsql(connection.ConnectionString).Options;
        await using (var db = new AccessDbContext(options))
        {
            await db.Database.ExecuteSqlRawAsync("CREATE TABLE Existing (Value TEXT); INSERT INTO Existing VALUES ('keep');");
            await db.InitializeSchemaAsync();
            await db.InitializeSchemaAsync();
            db.Accounts.Add(first);
            await db.SaveChangesAsync();
        }
        await using var reloaded = new AccessDbContext(options);
        var restored = await reloaded.Accounts.SingleAsync();
        Assert.True(restored.VerifyPassword(password));
        Assert.Equal("keep", await reloaded.Database.SqlQueryRaw<string>("SELECT value AS \"Value\" FROM Existing").SingleAsync());
        var columns = await reloaded.Database.SqlQueryRaw<string>("SELECT column_name AS \"Value\" FROM information_schema.columns WHERE table_schema='public' AND table_name='AccessAccounts'").ToListAsync();
        Assert.DoesNotContain("Password", columns);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FiveFailedPasswordsPersistLockout(bool concurrent)
    {
        await using var connection = new TestPostgresDatabase();
        await connection.OpenAsync();
        await using (var personnel = new PersonnelDbContext(new DbContextOptionsBuilder<PersonnelDbContext>().UseNpgsql(connection.ConnectionString).Options))
        {
            await PersonnelSchema.InitializeAsync(personnel);
        }
        var services = new ServiceCollection();
        services.AddDbContext<AccessDbContext>(options => options.UseNpgsql(connection.ConnectionString));
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<ILoginAccounts, PostgresLoginAccounts>();
        await using var provider = services.BuildServiceProvider();
        var id = Guid.NewGuid();
        await using (var personnel = new PersonnelDbContext(new DbContextOptionsBuilder<PersonnelDbContext>().UseNpgsql(connection.ConnectionString).Options))
        {
            var person = new ARGOS.Domain.Aggregates.Personnel.InstitutionalPerson(
                ARGOS.Domain.Aggregates.Organization.ValueObjects.OrganizationId.From(Guid.NewGuid()),
                new ARGOS.Domain.Aggregates.Personnel.OfficialIdentity("CR", "ID", "001"), "Test");
            id = person.Id;
            personnel.People.Add(person);
            await personnel.SaveChangesAsync();
        }
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
            await db.InitializeSchemaAsync();
            db.Accounts.Add(AccessAccount.Provision(id, "001", "valid-test-password", VerificationChannel.Sms,
                "+50688881234", "test-proof"));
            await db.SaveChangesAsync();
        }
        var login = provider.GetRequiredService<ILoginAccounts>();
        if (concurrent)
        {
            var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => login.ValidatePasswordAsync("001", "wrong", default)));
            Assert.All(results, result => Assert.Null(result));
        }
        else
            for (var attempt = 0; attempt < 5; attempt++)
                Assert.Null(await login.ValidatePasswordAsync("001", "wrong", default));
        Assert.Null(await login.ValidatePasswordAsync("001", "valid-test-password", default));
        await using var check = provider.CreateAsyncScope();
        var account = await check.ServiceProvider.GetRequiredService<AccessDbContext>().Accounts.SingleAsync();
        Assert.True(account.LockedUntilTicks > DateTimeOffset.UtcNow.UtcTicks);
        Assert.Equal(0, account.FailedAttempts);
    }
}
