using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Xml.Linq;

namespace ARGOS.Setup;

internal static class LocalSecrets
{
    internal static string PathName => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ARGOS", "Credentials", "ARGOS_PRUEBAS_20260917", "sistema.argosp.gmail.dpapi");
    internal static string? Load(string? path = null)
    {
        path ??= PathName;
        byte[] encrypted;
        var encoding = Encoding.UTF8;
        if (File.Exists(path)) encrypted = File.ReadAllBytes(path);
        else
        {
            var legacy = Path.ChangeExtension(path, ".xml");
            if (!File.Exists(legacy)) return null;
            var doc = XDocument.Load(legacy);
            var value = doc.Descendants().Single(e => e.Name.LocalName == "SS" && (string?)e.Attribute("N") == "Password").Value;
            encrypted = Convert.FromHexString(value);
            encoding = Encoding.Unicode;
        }
        var bytes = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
        try { return encoding.GetString(bytes); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    internal static void Save(string value, string? path = null)
    {
        path ??= PathName;
        var bytes = Encoding.UTF8.GetBytes(value);
        try
        {
            var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            var directory = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var acl = new DirectorySecurity();
            acl.SetAccessRuleProtection(true,false);
            foreach (var sid in new[]{WindowsIdentity.GetCurrent().User!,new SecurityIdentifier(WellKnownSidType.LocalSystemSid,null)})
                acl.AddAccessRule(new FileSystemAccessRule(sid,FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
            directory.SetAccessControl(acl);
            // Same-directory replacement prevents partial credential files after interruption.
            var temporary = path + ".new";
            File.WriteAllBytes(temporary, encrypted);
            File.Move(temporary, path, true);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
}
