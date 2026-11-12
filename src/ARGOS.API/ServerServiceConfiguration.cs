using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Hosting.WindowsServices;

namespace ARGOS.API;

/// <summary>Machine protected configuration for unattended Windows deployments.</summary>
public static class ServerServiceConfiguration
{
    public static IDisposable? Load(ConfigurationManager configuration)
    {
        var path = configuration["service-config"];
        if (string.IsNullOrWhiteSpace(path))
        {
            if (WindowsServiceHelpers.IsWindowsService())
                throw new InvalidOperationException("El servicio requiere configuración protegida explícita.");
            return null;
        }
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("La configuración protegida requiere Windows.");
        if (!Path.IsPathFullyQualified(path))
            throw new InvalidOperationException("La ruta de configuración debe ser absoluta.");

        // The directory ACL must admit only administrators, SYSTEM and the service identity.
        // Never delete this lock file: its open handle, not its existence, owns the lease.
        var lease = new FileStream(path + ".lock", FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.None);
        byte[]? plain = null;
        try
        {
            plain = ProtectedData.Unprotect(File.ReadAllBytes(path), null,
                DataProtectionScope.LocalMachine);
            var values = JsonSerializer.Deserialize<Dictionary<string, string?>>(plain)
                ?? throw new InvalidOperationException("Configuración vacía.");
            Validate(values);
            configuration.AddInMemoryCollection(values);
            return lease;
        }
        catch
        {
            lease.Dispose();
            // Do not disclose secret values or parsing diagnostics in startup logs.
            throw new InvalidOperationException("No se pudo cargar la configuración protegida del servidor.");
        }
        finally
        {
            if (plain != null) CryptographicOperations.ZeroMemory(plain);
        }
    }

    public static void Validate(IReadOnlyDictionary<string, string?> values)
    {
        foreach (var key in new[] { "ConnectionStrings:DefaultConnection",
                     "Kestrel:Endpoints:Https:Url", "Kestrel:Endpoints:Https:Certificate:Path" })
            if (!values.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException("Falta configuración obligatoria del servidor.");
        if (!Uri.TryCreate(values["Kestrel:Endpoints:Https:Url"], UriKind.Absolute, out var uri)
            || uri.Scheme != "https")
            throw new InvalidOperationException("El servidor requiere HTTPS explícito.");
        if (!Path.IsPathFullyQualified(values["Kestrel:Endpoints:Https:Certificate:Path"]!))
            throw new InvalidOperationException("El certificado requiere una ruta absoluta.");
        if (values.Keys.Any(k => k.StartsWith("Kestrel:Endpoints:", StringComparison.OrdinalIgnoreCase)
            && !k.StartsWith("Kestrel:Endpoints:Https:", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Solo se permite el endpoint HTTPS configurado.");
    }
}
