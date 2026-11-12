using System.Diagnostics;
using System.IO;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;
using Npgsql;

namespace ARGOS.Server;
public static class DatabaseBackup
{
    public static async Task<string> CreateAsync(string connectionString, string postgresBin, string destination, string recoveryPassphrase)
    {
        var settings = new NpgsqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(settings.Database) || string.IsNullOrWhiteSpace(settings.Username))
            throw new InvalidOperationException("Indique base y usuario.");
        foreach (var tool in new[] { "pg_dump.exe", "pg_restore.exe" })
            if (!File.Exists(Path.Combine(postgresBin, tool))) throw new InvalidOperationException("Seleccione la carpeta bin de PostgreSQL con pg_dump y pg_restore.");
        if (!Path.IsPathFullyQualified(destination) || !Directory.Exists(destination) || destination.StartsWith(@"\\"))
            throw new InvalidOperationException("Seleccione una carpeta local existente en un disco protegido.");
        await InstallationPreflight.CheckDatabaseAsync(connectionString);
        var folder = Path.Combine(destination, "ARGOS-respaldo-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"));
        var acl = new DirectorySecurity();
        acl.SetAccessRuleProtection(true, false);
        foreach (var sid in new[] { WindowsIdentity.GetCurrent().User!, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null) })
            acl.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(folder).Create(acl);
        var partial = Path.Combine(folder, "database.partial");
        try
        {
            await RunAsync(postgresBin, "pg_dump.exe", settings, new[] { "--no-password", "--format=custom", "--file", partial });
            await RunAsync(postgresBin, "pg_restore.exe", null, new[] { "--list", partial });
            string hash;
            await using (var input = File.OpenRead(partial)) hash = Convert.ToHexString(await SHA256.HashDataAsync(input));
            var encrypted = Path.Combine(folder, "database.argosbackup");
            await PortableBackupCrypto.EncryptAsync(partial, encrypted, recoveryPassphrase);
            File.Delete(partial);
            var manifest = new { format = 2, database = settings.Database, createdUtc = DateTime.UtcNow, sha256 = hash,
                scope = "Solo PostgreSQL. No incluye archivos externos, KMS, certificados ni configuración del servicio.",
                verification = "El contenido original fue listado por pg_restore antes de cifrarse; no equivale a restauración comprobada.",
                encrypted = true, encryption = "AES-256-CBC + HMAC-SHA256; PBKDF2-SHA256 (210000 iteraciones)." };
            await File.WriteAllTextAsync(Path.Combine(folder, "manifest.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
            return folder;
        }
        catch
        {
            // Keep incomplete data in its restricted folder for diagnosis, never mark it successful.
            throw new InvalidOperationException("No se completó el respaldo. Puede existir un archivo parcial en la carpeta elegida; no lo use como copia válida. La base de origen no se modificó.");
        }
    }

    public static async Task RestoreAsync(string encryptedBackup, string recoveryPassphrase, string postgresBin, string targetConnection)
    {
        if (!File.Exists(encryptedBackup) || !encryptedBackup.EndsWith(".argosbackup", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Seleccione un archivo .argosbackup existente.");
        foreach (var tool in new[] { "pg_restore.exe" })
            if (!File.Exists(Path.Combine(postgresBin, tool))) throw new InvalidOperationException("Seleccione la carpeta bin de PostgreSQL con pg_restore.");
        var settings = new NpgsqlConnectionStringBuilder(targetConnection);
        if (string.IsNullOrWhiteSpace(settings.Database) || string.IsNullOrWhiteSpace(settings.Username))
            throw new InvalidOperationException("Indique explícitamente la base destino y su cuenta PostgreSQL.");
        await InstallationPreflight.CheckDatabaseAsync(targetConnection);
        var temporary = Path.Combine(Path.GetDirectoryName(encryptedBackup)!, ".restore-" + Guid.NewGuid().ToString("N") + ".partial");
        try
        {
            await PortableBackupCrypto.DecryptAsync(encryptedBackup, temporary, recoveryPassphrase);
            await RunAsync(postgresBin, "pg_restore.exe", null, new[] { "--list", temporary });
            await RunAsync(postgresBin, "pg_restore.exe", settings, new[] { "--no-password", "--no-owner", "--no-privileges", "--exit-on-error", temporary });
        }
        catch (InvalidOperationException) { throw; }
        catch { throw new InvalidOperationException("No se completó la restauración. Revise la clave, el respaldo, la base destino y las herramientas PostgreSQL."); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static async Task RunAsync(string bin, string tool, NpgsqlConnectionStringBuilder? settings, IEnumerable<string> arguments)
    {
        var start = new ProcessStartInfo(Path.Combine(bin, tool))
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        // Do not inherit a service file, password file or other connection overrides from the shell.
        foreach (var key in start.Environment.Keys.Where(k => k.StartsWith("PG", StringComparison.OrdinalIgnoreCase)).ToArray()) start.Environment.Remove(key);
        if (settings != null)
        {
            start.Environment["PGHOST"] = settings.Host;
            start.Environment["PGPORT"] = settings.Port.ToString();
            start.Environment["PGDATABASE"] = settings.Database;
            start.Environment["PGUSER"] = settings.Username;
            start.Environment["PGPASSWORD"] = settings.Password;
            start.Environment["PGCONNECT_TIMEOUT"] = "10";
            start.Environment["PGAPPNAME"] = "ARGOS-Backup";
            start.Environment["PGSSLMODE"] = settings.SslMode.ToString().ToLowerInvariant().Replace("verifyfull", "verify-full").Replace("verifyca", "verify-ca");
        }
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("No se pudo iniciar la herramienta PostgreSQL.");
        start.Environment.Remove("PGPASSWORD");
        var output = DrainAsync(process.StandardOutput);
        var error = DrainAsync(process.StandardError);
        using var timeout = new CancellationTokenSource(TimeSpan.FromHours(2));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch { if (!process.HasExited) process.Kill(true); await process.WaitForExitAsync(); throw; }
        finally { await Task.WhenAll(output, error); }
        if (process.ExitCode != 0) throw new InvalidOperationException("La herramienta PostgreSQL informó un fallo.");
    }
    private static async Task DrainAsync(StreamReader reader)
    {
        var buffer = new char[4096];
        while (await reader.ReadAsync(buffer) != 0) { }
    }
}
