using System.Diagnostics;
using System.IO;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;

namespace ARGOS.Server;

/// <summary>Stores a daily-backup request under LocalMachine DPAPI and runs it as SYSTEM.</summary>
public sealed record ScheduledBackupRequest(
    string Name, string ConnectionString, string PostgresBin, string Destination,
    string RecoveryPassphrase, TimeOnly Time);

public static class ScheduledBackup
{
    private const string Prefix = "ARGOS-Backup-";

    public static string Configure(ScheduledBackupRequest request, string executable)
    {
        Validate(request);
        if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
            throw new InvalidOperationException("Windows requiere autorización de administrador para programar el respaldo.");
        if (!File.Exists(executable)) throw new InvalidOperationException("No se encontró el ejecutable de ARGOS.");

        var configuration = ConfigurationPath(request.Name);
        Directory.CreateDirectory(Path.GetDirectoryName(configuration)!);
        var payload = JsonSerializer.SerializeToUtf8Bytes(request);
        try
        {
            var encrypted = ProtectedData.Protect(payload, null, DataProtectionScope.LocalMachine);
            try { File.WriteAllBytes(configuration, encrypted); }
            finally { CryptographicOperations.ZeroMemory(encrypted); }
        }
        finally { CryptographicOperations.ZeroMemory(payload); }
        ProtectDirectory(Path.GetDirectoryName(configuration)!);

        var command = Quote(executable) + " --run-scheduled-backup " + Quote(configuration);
        RunSchtasks("/Create", "/TN", Prefix + SafeName(request.Name), "/SC", "DAILY", "/ST", request.Time.ToString("HH:mm"), "/RU", "SYSTEM", "/RL", "HIGHEST", "/TR", command, "/F");
        return configuration;
    }

    public static async Task<int> RunAsync(string configuration)
    {
        try
        {
            var request = Load(configuration);
            Validate(request);
            var folder = await DatabaseBackup.CreateAsync(request.ConnectionString, request.PostgresBin, request.Destination, request.RecoveryPassphrase);
            WriteStatus(request.Name, true, "Respaldo cifrado creado.", folder);
            return 0;
        }
        catch
        {
            var name = Path.GetFileNameWithoutExtension(configuration);
            WriteStatus(name, false, "El respaldo programado no se completó. Revise el estado desde el panel ARGOS.", null);
            return 1;
        }
    }

    public static void Validate(ScheduledBackupRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || SafeName(request.Name).Length < 2) throw new InvalidOperationException("Indique un nombre válido para el respaldo.");
        if (string.IsNullOrWhiteSpace(request.ConnectionString) || string.IsNullOrWhiteSpace(request.PostgresBin) || string.IsNullOrWhiteSpace(request.Destination)) throw new InvalidOperationException("Complete conexión, herramientas PostgreSQL y destino.");
        if (request.RecoveryPassphrase.Length < 12) throw new InvalidOperationException("La clave de recuperación debe tener al menos 12 caracteres.");
    }

    private static ScheduledBackupRequest Load(string configuration)
    {
        var encrypted = File.ReadAllBytes(configuration);
        var payload = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.LocalMachine);
        try { return JsonSerializer.Deserialize<ScheduledBackupRequest>(payload) ?? throw new InvalidOperationException("Configuración inválida."); }
        finally { CryptographicOperations.ZeroMemory(encrypted); CryptographicOperations.ZeroMemory(payload); }
    }
    private static string ConfigurationPath(string name) => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ARGOS", "Backups", SafeName(name) + ".dpapi");
    private static string SafeName(string value) => new string(value.Trim().Where(char.IsLetterOrDigit).ToArray());
    private static string Quote(string value) => "\"" + value.Replace("\"", "") + "\"";
    private static void RunSchtasks(params string[] arguments)
    {
        var start = new ProcessStartInfo("schtasks.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("No se pudo iniciar el programador de tareas.");
        process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException("Windows no pudo programar el respaldo. Revise que esta cuenta tenga autorización administrativa.");
    }
    private static void ProtectDirectory(string directory)
    {
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(true, false);
        security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), FileSystemRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), FileSystemRights.FullControl, AccessControlType.Allow));
        new DirectoryInfo(directory).SetAccessControl(security);
    }
    private static void WriteStatus(string name, bool succeeded, string message, string? folder)
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ARGOS", "Backups", SafeName(name) + ".status.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(new { succeeded, message, folder, checkedAtUtc = DateTimeOffset.UtcNow }));
    }
}
