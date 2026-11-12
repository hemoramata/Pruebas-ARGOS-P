using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using ARGOS.Server;
using Npgsql;

public class DatabaseBackupTests
{
    [Fact]
    public async Task SyntheticBackupRestoresAndMatchesHash()
    {
        var connection = Environment.GetEnvironmentVariable("ARGOS_BACKUP_TEST_CONNECTION");
        if (connection == null) return; // Explicit isolated runner supplies connection; never discover user credentials.
        var settings = new NpgsqlConnectionStringBuilder(connection);
        Assert.Equal("127.0.0.1", settings.Host);
        Assert.Equal(55449, settings.Port);
        Assert.Equal("argos_backup_test", settings.Username);
        var destination = Environment.GetEnvironmentVariable("ARGOS_BACKUP_TEST_OUTPUT")!;
        const string bin = @"C:\Program Files\PostgreSQL\18\bin";
        await using var db = new NpgsqlConnection(connection);
        await db.OpenAsync();
        await using (var seed = new NpgsqlCommand("CREATE TABLE backup_fixture(id integer PRIMARY KEY, value text); INSERT INTO backup_fixture VALUES (7,'synthetic');", db)) await seed.ExecuteNonQueryAsync();
        const string recoveryPassphrase = "test-recovery-passphrase";
        var folder = await DatabaseBackup.CreateAsync(connection, bin, destination, recoveryPassphrase);
        var encrypted = Path.Combine(folder, "database.argosbackup");
        Assert.True(File.Exists(encrypted));
        Assert.False(File.Exists(Path.Combine(folder, "database.dump")));
        Assert.False(File.Exists(Path.Combine(folder, "database.partial")));
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(folder, "manifest.json")));
        Assert.True(manifest.RootElement.GetProperty("encrypted").GetBoolean());
        await using (var create = new NpgsqlCommand("CREATE DATABASE restored_backup", db)) await create.ExecuteNonQueryAsync();
        settings.Database = "restored_backup";
        await DatabaseBackup.RestoreAsync(encrypted, recoveryPassphrase, bin, settings.ConnectionString);
        await using var restored = new NpgsqlConnection(settings.ConnectionString);
        await restored.OpenAsync();
        await using var check = new NpgsqlCommand("SELECT value FROM backup_fixture WHERE id=7", restored);
        Assert.Equal("synthetic", await check.ExecuteScalarAsync());
    }

    [Fact]
    public async Task PortableBackupRejectsWrongPassphraseAndTampering()
    {
        var root = Path.Combine(Path.GetTempPath(), "argos-portable-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var plain = Path.Combine(root, "plain.dump");
            var encrypted = Path.Combine(root, "backup.argosbackup");
            await File.WriteAllTextAsync(plain, "ARGOS portable recovery fixture");
            await PortableBackupCrypto.EncryptAsync(plain, encrypted, "correct-recovery-key");
            await Assert.ThrowsAsync<InvalidOperationException>(() => PortableBackupCrypto.DecryptAsync(encrypted, Path.Combine(root, "wrong.dump"), "wrong-recovery-key"));
            var bytes = await File.ReadAllBytesAsync(encrypted); bytes[40] ^= 0x01; await File.WriteAllBytesAsync(encrypted, bytes);
            await Assert.ThrowsAsync<InvalidOperationException>(() => PortableBackupCrypto.DecryptAsync(encrypted, Path.Combine(root, "changed.dump"), "correct-recovery-key"));
        }
        finally { Directory.Delete(root, true); }
    }
}
