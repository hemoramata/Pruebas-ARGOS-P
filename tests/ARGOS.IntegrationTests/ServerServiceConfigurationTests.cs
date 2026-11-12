using ARGOS.API;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text.Json;

public class ServerServiceConfigurationTests
{
    private static Dictionary<string, string?> Valid() => new()
    {
        ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=synthetic",
        ["Kestrel:Endpoints:Https:Url"] = "https://localhost:7449",
        ["Kestrel:Endpoints:Https:Certificate:Path"] = Path.GetFullPath("synthetic.pfx")
    };

    [Fact]
    public void InteractiveModeDoesNotRequireServiceSecrets()
        => Assert.Null(ServerServiceConfiguration.Load(new ConfigurationManager()));

    [Fact]
    public void RejectsMissingConfigurationAndPlainHttp()
    {
        Assert.Throws<InvalidOperationException>(() => ServerServiceConfiguration.Validate(new Dictionary<string, string?>()));
        var values = Valid();
        values["Kestrel:Endpoints:Https:Url"] = "http://localhost:7449";
        Assert.Throws<InvalidOperationException>(() => ServerServiceConfiguration.Validate(values));
        values = Valid();
        values["Kestrel:Endpoints:Http:Url"] = "http://localhost:7448";
        Assert.Throws<InvalidOperationException>(() => ServerServiceConfiguration.Validate(values));
    }

    [Fact]
    public void ProtectedConfigurationIsLoadedAndLeaseIsExclusiveUntilDisposed()
    {
        if (!OperatingSystem.IsWindows()) return;
        var folder = Path.Combine(Path.GetTempPath(), "argos-service-test-" + Guid.NewGuid());
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, "synthetic.dpapi");
        try
        {
            File.WriteAllBytes(file, ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(Valid()), null, DataProtectionScope.LocalMachine));
            var config = new ConfigurationManager();
            config["service-config"] = file;
            using (ServerServiceConfiguration.Load(config))
            {
                Assert.Equal("Host=localhost;Database=synthetic", config.GetConnectionString("DefaultConnection"));
                Assert.Throws<IOException>(() => ServerServiceConfiguration.Load(config));
            }
            using var reopened = ServerServiceConfiguration.Load(config);
            Assert.NotNull(reopened);
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public void DamagedSecretFailsClosedAndReleasesLease()
    {
        if (!OperatingSystem.IsWindows()) return;
        var folder = Path.Combine(Path.GetTempPath(), "argos-service-test-" + Guid.NewGuid());
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, "synthetic.dpapi");
        try
        {
            File.WriteAllText(file, "not-a-secret");
            var config = new ConfigurationManager();
            config["service-config"] = file;
            var error = Assert.Throws<InvalidOperationException>(() => ServerServiceConfiguration.Load(config));
            Assert.DoesNotContain("not-a-secret", error.ToString());
            using var lease = new FileStream(file + ".lock", FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        finally { Directory.Delete(folder, true); }
    }
}
