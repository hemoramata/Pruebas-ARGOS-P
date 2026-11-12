using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ARGOS.Server;

public class LocalTestCertificateTests
{
    [Fact]
    public void LocalCertificateIsPasswordProtectedScopedAndRemovedAfterUse()
    {
        string path;
        using (var generated = LocalTestCertificate.Create())
        {
            path = generated.Path;
            using var cert = X509CertificateLoader.LoadPkcs12FromFile(path, generated.Password, X509KeyStorageFlags.EphemeralKeySet);
            Assert.True(cert.HasPrivateKey);
            Assert.True(cert.MatchesHostname("localhost", allowCommonName: false));
            Assert.False(cert.MatchesHostname("institution.example", allowCommonName: false));
            Assert.InRange((cert.NotAfter.ToUniversalTime() - DateTime.UtcNow).TotalDays, 29, 31);
            Assert.Throws<CryptographicException>(() => X509CertificateLoader.LoadPkcs12FromFile(path, "incorrect", X509KeyStorageFlags.EphemeralKeySet));
        }
        Assert.False(File.Exists(path));
    }
}
