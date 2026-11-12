using System.Security.Cryptography;
using System.Text.Json;
using ARGOS.Server;

if (args.Length != 3) throw new InvalidOperationException("Uso interno: config, postgres-bin, destino.");
var encrypted = ProtectedData.Unprotect(await File.ReadAllBytesAsync(args[0]), null, DataProtectionScope.LocalMachine);
try
{
    var values = JsonSerializer.Deserialize<Dictionary<string, string?>>(encrypted) ?? throw new InvalidOperationException("Configuración no disponible.");
    if (!values.TryGetValue("ConnectionStrings:DefaultConnection", out var connection) || string.IsNullOrWhiteSpace(connection))
        throw new InvalidOperationException("No hay conexión de pruebas.");
    var passphrase = Convert.ToBase64String(RandomNumberGenerator.GetBytes(36));
    var folder = await DatabaseBackup.CreateAsync(connection, args[1], args[2], passphrase);
    var portable = Path.Combine(folder, "database.argosbackup");
    var temporaryDump = Path.Combine(folder, "recovery-validation.dump");
    await PortableBackupCrypto.DecryptAsync(portable, temporaryDump, passphrase);
    Console.WriteLine(folder);
}
finally { CryptographicOperations.ZeroMemory(encrypted); }
