using System.Security.Claims;
using ARGOS.API;
using ARGOS.Application.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ARGOS.UnitTests;

public class InitialPasswordGateTests
{
    [Theory]
    [InlineData(true, true, false, false)]
    [InlineData(false, false, false, true)]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, true, false)]
    public async Task CookieValidationRejectsStalePasswordStateAndRefreshesPermissions(
        bool storedMustChange, bool cookieMustChange, bool rejected, bool hasPermission)
    {
        var person = Guid.NewGuid(); var organization = Guid.NewGuid(); var session = Guid.NewGuid();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ILoginAccounts>(new Accounts(new LoginAccount(person, organization, "Test", "masked",
            VerificationChannel.InstitutionalEmail, new[] { "case-files.finalize" }, storedMustChange)));
        services.AddPersonnelSecurity();
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<ActiveSessions>().Add(session, person);
        var http = new DefaultHttpContext { RequestServices = provider };
        http.Request.Scheme = "https";
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] {
            new Claim(ClaimTypes.NameIdentifier, person.ToString()),
            new Claim("organization_id", organization.ToString()), new Claim("session_id", session.ToString()),
            new Claim("must_change_password", cookieMustChange ? "true" : "false"),
            new Claim("permission", "stale.permission")
        }, "Cookies"));
        var options = provider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get("Cookies");
        var context = new CookieValidatePrincipalContext(http,
            new AuthenticationScheme("Cookies", null, typeof(CookieAuthenticationHandler)), options,
            new AuthenticationTicket(principal, new AuthenticationProperties(), "Cookies"));
        await options.Events.ValidatePrincipal(context);
        Assert.Equal(rejected, context.Principal is null);
        Assert.Equal(hasPermission, context.Principal?.HasClaim("permission", "case-files.finalize") ?? false);
        Assert.False(context.Principal?.HasClaim("permission", "stale.permission") ?? false);
    }

    private sealed class Accounts(LoginAccount account) : ILoginAccounts
    {
        public Task<LoginAccount?> GetActiveAsync(Guid id, CancellationToken ct) => Task.FromResult<LoginAccount?>(account);
        public Task<LoginAccount?> ValidatePasswordAsync(string id, string password, CancellationToken ct) => Task.FromResult<LoginAccount?>(account);
    }

    [Theory]
    [InlineData("POST", "/api/case-files/123/finalize", true, false)]
    [InlineData("POST", "/api/superuser/authorize", true, false)]
    [InlineData("GET", "/api/case-files", true, false)]
    [InlineData("POST", "/api/session/password/change", true, true)]
    [InlineData("POST", "/api/session/logout", true, true)]
    [InlineData("GET", "/api/session/status", true, true)]
    [InlineData("GET", "/login/", true, true)]
    [InlineData("GET", "/login/index.html", true, true)]
    [InlineData("GET", "/login/app.js", true, true)]
    [InlineData("POST", "/login/", true, false)]
    [InlineData("GET", "/setup/", true, false)]
    [InlineData("POST", "/api/case-files/123/finalize", false, true)]
    public async Task ProvisionalSessionCannotReachBusinessEndpointsEvenWithPermissionClaims(
        string method, string path, bool mustChange, bool allowed)
    {
        var http = new DefaultHttpContext();
        http.Request.Method = method;
        http.Request.Path = path;
        http.Response.Body = new MemoryStream();
        http.User = new ClaimsPrincipal(new ClaimsIdentity(new[] {
            new Claim("must_change_password", mustChange ? "true" : "false"),
            new Claim("amr", "mfa"), new Claim("permission", "case-files.finalize")
        }, "test"));
        var reached = false;
        await new InitialPasswordGate(_ => { reached = true; return Task.CompletedTask; }).InvokeAsync(http);
        Assert.Equal(allowed, reached);
        Assert.Equal(allowed ? 200 : 403, http.Response.StatusCode);
    }

    [Fact]
    public void PasswordChangeRevocationRemovesAllSessionsOfThatPersonOnly()
    {
        var sessions = new ActiveSessions(TimeProvider.System);
        var person = Guid.NewGuid(); var other = Guid.NewGuid();
        var first = Guid.NewGuid(); var second = Guid.NewGuid(); var third = Guid.NewGuid();
        sessions.Add(first, person); sessions.Add(second, person); sessions.Add(third, other);
        sessions.RevokeForPerson(person);
        Assert.False(sessions.Contains(first, person));
        Assert.False(sessions.Contains(second, person));
        Assert.True(sessions.Contains(third, other));
    }
}
