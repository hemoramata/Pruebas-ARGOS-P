using System.Net;
using System.Security.Claims;
using ARGOS.API;
using ARGOS.API.Endpoints;
using ARGOS.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ARGOS.IntegrationTests;

public sealed class PersonnelAuthorizationTests
{
    [Theory]
    [InlineData(false, false, false, true, 401)]
    [InlineData(true, false, true, true, 403)]
    [InlineData(true, true, false, true, 403)]
    [InlineData(true, true, true, false, 403)]
    public async Task PersonnelRequiresAuthenticatedMfaAdministratorAndOrganization(
        bool authenticated, bool mfa, bool permitted, bool organization, int expected)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddInfrastructure(TestPostgresDatabase.CreateConnectionString());
        builder.Services.AddPersonnelSecurity();
        await using var app = builder.Build();
        app.UseAuthentication();
        // Test-only trusted principal. Production has no header-based identity or bypass.
        app.Use(async (context, next) =>
        {
            if (authenticated)
            {
                var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "test-user") };
                if (mfa) claims.Add(new("amr", "mfa"));
                if (permitted) claims.Add(new("permission", "personnel.manage"));
                if (organization) claims.Add(new("organization_id", Guid.NewGuid().ToString()));
                context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test-only"));
            }
            await next(context);
        });
        app.UseAuthorization();
        app.MapPersonnelEndpoints();
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features
            .Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient { BaseAddress = new Uri(address) };
        using var response = await client.GetAsync($"/api/personnel/{Guid.NewGuid()}");
        Assert.Equal((HttpStatusCode)expected, response.StatusCode);
        await app.StopAsync();
    }
}
