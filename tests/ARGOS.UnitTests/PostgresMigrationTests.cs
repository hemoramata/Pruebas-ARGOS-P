using ARGOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ARGOS.UnitTests;

public sealed class PostgresMigrationTests
{
    [Theory]
    [InlineData("20260911115057_InitialPostgres", "0")]
    [InlineData("20260913000100_ProtectRetainedRecords", "20260911115057_InitialPostgres")]
    public void RetainedDataMigrationsRejectRollbackBeforeProducingSql(string from, string to)
    {
        using var db = new ArgosDesignFactory().CreateDbContext([]);
        var migrator = db.GetService<IMigrator>();
        Assert.Throws<NotSupportedException>(() => migrator.GenerateScript(from, to));
    }

    [Fact]
    public void AllContextsHavePostgresMigrationsMatchingTheirModels()
    {
        using var main = new ArgosDesignFactory().CreateDbContext([]);
        using var personnel = new PersonnelDesignFactory().CreateDbContext([]);
        using var access = new AccessDesignFactory().CreateDbContext([]);
        using var designation = new DesignationDesignFactory().CreateDbContext([]);
        foreach (var db in new DbContext[] { main, personnel, access, designation })
        {
            Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", db.Database.ProviderName);
            Assert.NotEmpty(db.Database.GetMigrations());
            Assert.False(db.Database.HasPendingModelChanges());
            var script = db.Database.GenerateCreateScript();
            Assert.Contains("uuid", script);
        }
    }
}
