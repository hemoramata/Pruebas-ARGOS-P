using System.Diagnostics;
using System.IO;
using System.Net.Mail;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ARGOS.Server;
public sealed record ServerInstallation(string Name, string Package, string Destination, string Url,
    string Certificate, string CertificatePassword, string Database, string Sender, string MailPassword)
{
    public override string ToString() => "Solicitud de instalación ARGOS (credenciales ocultas)";
}

public static class NativeServerInstaller
{
    public static void Validate(ServerInstallation request)
    {
        if (!Regex.IsMatch(request.Name, "^ARGOS-[A-Za-z0-9-]+$")) throw new InvalidOperationException("Nombre de servicio inválido.");
        if (!Path.IsPathFullyQualified(request.Destination) || request.Destination.Contains('"')
            || request.Destination.StartsWith(@"\\") || Directory.Exists(request.Destination) || File.Exists(request.Destination))
            throw new InvalidOperationException("Seleccione una carpeta local absoluta que todavía no exista.");
        if (!File.Exists(Path.Combine(request.Package, "ARGOS.API.exe")) || !File.Exists(Path.Combine(request.Package, "coreclr.dll")))
            throw new InvalidOperationException("Seleccione un paquete autónomo win-x64 publicado de ARGOS.API.");
        var packageRoot = Path.GetFullPath(request.Package).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if ((Path.GetFullPath(request.Destination).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar)
            .StartsWith(packageRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("El destino debe estar fuera de la carpeta del paquete.");
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var url) || url.Scheme != "https" || url.AbsolutePath != "/" || url.Query.Length != 0 || url.UserInfo.Length != 0)
            throw new InvalidOperationException("Introduzca una dirección HTTPS sin rutas, consultas ni credenciales.");
        if (!request.Database.StartsWith("Host=", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("La cadena PostgreSQL debe comenzar con Host= e incluir la base y credenciales.");
        if (!MailAddress.TryCreate(request.Sender, out _) || string.IsNullOrWhiteSpace(request.MailPassword))
            throw new InvalidOperationException("Complete el remitente y la contraseña de aplicación.");
        try
        {
            using var cert = X509CertificateLoader.LoadPkcs12FromFile(request.Certificate, request.CertificatePassword, X509KeyStorageFlags.EphemeralKeySet);
            if (!cert.HasPrivateKey || cert.NotAfter <= DateTime.Now || cert.NotBefore > DateTime.Now
                || cert.Extensions.Any(x => x.Oid?.Value == "1.3.6.1.4.1.311.84.1.1")
                || !cert.MatchesHostname(url.Host, allowWildcards: true, allowCommonName: false))
                throw new InvalidOperationException();
        }
        catch { throw new InvalidOperationException("Revise el certificado PFX, su contraseña, vigencia y nombre DNS. No se admite el certificado de desarrollo."); }
    }

    public static async Task InstallAsync(ServerInstallation request)
    {
        if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
            throw new InvalidOperationException("Se requiere autorización de administrador.");
        Validate(request);
        InstallationPreflight.CheckPort(request.Url);
        await InstallationPreflight.CheckDatabaseAsync(request.Database);
        InstallationPreflight.CheckPort(request.Url);
        var services = ServiceController.GetServices();
        try { if (services.Any(s => s.ServiceName.Equals(request.Name, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("El servicio ya existe."); }
        finally { foreach (var service in services) service.Dispose(); }
        // Restrict inheritance at creation, before writing binaries, certificate or secrets.
        var acl = new DirectorySecurity();
        acl.SetAccessRuleProtection(true, false);
        foreach (var sid in new[] { WellKnownSidType.LocalSystemSid, WellKnownSidType.BuiltinAdministratorsSid })
            acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(sid, null), FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        var root = new DirectoryInfo(request.Destination);
        root.Create(acl);
        var app = Path.Combine(root.FullName, "app");
        var state = Path.Combine(root.FullName, "state");
        CopyPackage(new DirectoryInfo(request.Package), new DirectoryInfo(app));
        Directory.CreateDirectory(state);
        var certificate = Path.Combine(state, "server.pfx");
        File.Copy(request.Certificate, certificate, false);
        var values = new Dictionary<string, string>
        {
            ["ConnectionStrings:DefaultConnection"] = request.Database,
            ["Kestrel:Endpoints:Https:Url"] = request.Url,
            ["Kestrel:Endpoints:Https:Certificate:Path"] = certificate,
            ["Kestrel:Endpoints:Https:Certificate:Password"] = request.CertificatePassword,
            ["Gmail:Sender"] = request.Sender,
            ["Gmail:AppPassword"] = request.MailPassword
        };
        var plain = JsonSerializer.SerializeToUtf8Bytes(values);
        var config = Path.Combine(state, "server.dpapi");
        try { File.WriteAllBytes(config, ProtectedData.Protect(plain, null, DataProtectionScope.LocalMachine)); }
        finally { CryptographicOperations.ZeroMemory(plain); values.Clear(); }
        var binary = $"\"{Path.Combine(app, "ARGOS.API.exe")}\" --contentRoot \"{app}\" --environment Production --service-config \"{config}\"";
        Sc("create", request.Name, "binPath=", binary, "start=", "demand", "obj=", @"NT SERVICE\" + request.Name);
        var identity = new NTAccount(@"NT SERVICE\" + request.Name);
        Grant(root, identity, FileSystemRights.ReadAndExecute, false);
        Grant(new DirectoryInfo(app), identity, FileSystemRights.ReadAndExecute, true);
        Grant(new DirectoryInfo(state), identity, FileSystemRights.Modify, true);
        Sc("failure", request.Name, "reset=", "86400", "actions=", "restart/60000/restart/120000/restart/300000");
        Sc("config", request.Name, "start=", "auto");
    }

    private static void CopyPackage(DirectoryInfo source, DirectoryInfo target)
    {
        if ((source.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("No se admiten enlaces en el paquete.");
        target.Create();
        foreach (var file in source.GetFiles())
        {
            if ((file.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("No se admiten enlaces en el paquete.");
            file.CopyTo(Path.Combine(target.FullName, file.Name), false);
        }
        foreach (var folder in source.GetDirectories()) CopyPackage(folder, new DirectoryInfo(Path.Combine(target.FullName, folder.Name)));
    }
    private static void Grant(DirectoryInfo directory, IdentityReference identity, FileSystemRights rights, bool inherit)
    {
        var acl = directory.GetAccessControl();
        acl.AddAccessRule(new FileSystemAccessRule(identity, rights,
            inherit ? InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit : InheritanceFlags.None,
            PropagationFlags.None, AccessControlType.Allow));
        directory.SetAccessControl(acl);
    }
    private static void Sc(params string[] args)
    {
        var info = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "sc.exe"))
        { UseShellExecute = false, CreateNoWindow = true };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        using var process = Process.Start(info) ?? throw new InvalidOperationException("No se pudo ejecutar el administrador de servicios.");
        process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException("Windows rechazó la operación de servicio.");
    }
}
