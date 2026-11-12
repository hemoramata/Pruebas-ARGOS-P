using System;
using System.Threading.Tasks;
using ARGOS.Server;

public class ServerDiagnosticsTests
{
    [Theory]
    [InlineData("{}")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{\"status\":\"Healthy\",\"service\":\"other\"}")]
    [InlineData("{\"status\":false,\"service\":\"ARGOS.API\"}")]
    public void RejectsUnrelatedHealth(string body) => Assert.False(ServerDiagnostics.IsArgosHealth(body));
    [Fact]
    public void AcceptsArgosHealth() => Assert.True(ServerDiagnostics.IsArgosHealth("{\"status\":\"Healthy\",\"service\":\"ARGOS.API\"}"));
    [Fact]
    public void WarnsAboutExpiringCertificate()
    {
        var now = DateTime.UtcNow;
        Assert.Contains("Atención", ServerDiagnostics.ExpiryMessage(now.AddDays(10), now));
        Assert.Contains("vencido", ServerDiagnostics.ExpiryMessage(now.AddDays(-1), now));
        Assert.DoesNotContain("Atención", ServerDiagnostics.ExpiryMessage(now.AddDays(90), now));
    }
    [Theory]
    [InlineData("http://localhost:7443")]
    [InlineData("https://user:password@localhost:7443")]
    [InlineData("https://localhost/login/")]
    public async Task RejectsInvalidAddressBeforeNetwork(string address)
        => Assert.False((await ServerDiagnostics.CheckAsync(address)).Healthy);
}
