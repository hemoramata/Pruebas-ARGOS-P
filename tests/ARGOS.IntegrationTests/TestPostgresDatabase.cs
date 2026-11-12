using System.Collections.Concurrent;
using ARGOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ARGOS.IntegrationTests;

/// <summary>Databases belong exclusively to the temporary cluster started by the test runner.</summary>
internal sealed class TestPostgresDatabase : IAsyncDisposable
{
    private static readonly ConcurrentDictionary<string, bool> Created = new();
    private static readonly SemaphoreSlim Gate = new(1);
    public string ConnectionString { get; } = CreateConnectionString();

    public static string CreateConnectionString()
    {
        var builder = Admin();
        builder.Database = "argos_test_" + Guid.NewGuid().ToString("N");
        builder.Pooling = false;
        var connection = builder.ConnectionString;
        Created.TryAdd(connection, false);
        return connection;
    }

    private static NpgsqlConnectionStringBuilder Admin()
    {
        var value = Environment.GetEnvironmentVariable("ARGOS_TEST_POSTGRES_ADMIN")
            ?? throw new InvalidOperationException("Run tools/test-postgres-migrations.ps1 -IntegrationTests to use an isolated PostgreSQL cluster.");
        var builder = new NpgsqlConnectionStringBuilder(value);
        if (builder.Host != "127.0.0.1" || builder.Port == 5432 || builder.Username != "argos_test"
            || builder.Database != "postgres")
            throw new InvalidOperationException("Integration tests require the dedicated temporary cluster.");
        return builder;
    }

    public Task OpenAsync() => EnsureCreatedAsync(ConnectionString);

    private static async Task EnsureCreatedAsync(string connection)
    {
        if (!Created.ContainsKey(connection)) throw new InvalidOperationException("Database was not allocated by this fixture.");
        await Gate.WaitAsync();
        try
        {
            if (Created[connection]) return;
            var name = new NpgsqlConnectionStringBuilder(connection).Database!;
            await using var admin = new NpgsqlConnection(Admin().ConnectionString);
            await admin.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin);
            await command.ExecuteNonQueryAsync();
            Created[connection] = true;
        }
        finally { Gate.Release(); }
    }

    public static async Task InitializeAsync(string connection)
    {
        await EnsureCreatedAsync(connection);
        await using var main = new ArgosDbContext(new DbContextOptionsBuilder<ArgosDbContext>().UseNpgsql(connection).Options);
        await using var personnel = new PersonnelDbContext(new DbContextOptionsBuilder<PersonnelDbContext>().UseNpgsql(connection).Options);
        await using var access = new AccessDbContext(new DbContextOptionsBuilder<AccessDbContext>().UseNpgsql(connection).Options);
        await using var designation = new DesignationDbContext(new DbContextOptionsBuilder<DesignationDbContext>().UseNpgsql(connection).Options);
        foreach (var db in new DbContext[] { main, personnel, access, designation }) await db.Database.MigrateAsync();
    }

    // The runner stops the entire disposable cluster; no destructive database cleanup is issued.
    public static Task ReleaseAsync(string connection)
    {
        if (!Created.ContainsKey(connection)) throw new InvalidOperationException("Unknown test database.");
        return Task.CompletedTask;
    }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
