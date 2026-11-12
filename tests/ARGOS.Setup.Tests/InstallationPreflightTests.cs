using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using ARGOS.Server;

public class InstallationPreflightTests
{
    [Fact]
    public void OccupiedPortIsRejectedWithoutStoppingListener()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            Assert.Throws<InstallationPreflightException>(() => InstallationPreflight.CheckPort($"https://localhost:{port}"));
            Assert.True(listener.Server.IsBound);
        }
        finally { listener.Stop(); }
    }
    [Fact]
    public async Task InvalidConnectionDoesNotDiscloseInput()
    {
        var error = await Assert.ThrowsAsync<InstallationPreflightException>(() => InstallationPreflight.CheckDatabaseAsync("synthetic-sensitive-value"));
        Assert.DoesNotContain("synthetic-sensitive-value", error.ToString());
    }
    [Fact]
    public async Task MissingDatabaseFailsBeforeConnecting()
    {
        var error = await Assert.ThrowsAsync<InstallationPreflightException>(() => InstallationPreflight.CheckDatabaseAsync("Host=localhost;Username=synthetic"));
        Assert.Contains("nombre de la base", error.Message);
    }
}
