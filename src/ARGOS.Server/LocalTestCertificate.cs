using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace ARGOS.Server;

/// <summary>Temporary encrypted PFX. Never installs a trust anchor.</summary>
public sealed class LocalTestCertificate : IDisposable
{
    public string Path { get; }
    public string Password { get; }
    private LocalTestCertificate(string path, string password) { Path = path; Password = password; }
    public static LocalTestCertificate Create()
    {
        using var rsa = RSA.Create(3072);
        var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName("localhost");
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new("1.3.6.1.5.5.7.3.1") }, true));
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(30));
        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "argos-local-test-" + Guid.NewGuid().ToString("N") + ".pfx");
        var bytes = certificate.Export(X509ContentType.Pfx, password);
        try
        {
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            stream.Write(bytes);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
        return new LocalTestCertificate(path, password);
    }
    public void Dispose()
    {
        // Only the exact temporary file created by this instance is removed.
        try { File.Delete(Path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
