using System.Net;
using System.Net.Http.Json;
using ARGOS.API;
using ARGOS.API.Endpoints;
using ARGOS.Application.Security;
using ARGOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ARGOS.IntegrationTests;

public sealed partial class LoginEndpointsTests
{
    [Fact]
    public async Task HomeProfileRequiresSessionAndReflectsRevokedPermissions()
    {
        await using var host=await Host.Start(true,false);
        Assert.Equal(HttpStatusCode.Unauthorized,(await host.Client.GetAsync("/api/session/profile")).StatusCode);
        var begin=await host.Client.PostAsJsonAsync("/api/session/begin",new {Identification="test",Password="test"});
        var pending=(await begin.Content.ReadFromJsonAsync<LoginChallenge>())!;
        var verified=await host.Client.PostAsJsonAsync("/api/session/verify",new {pending.Id,pending.Binding,Code=host.Adapter.Code});
        host.Client.DefaultRequestHeaders.Add("Cookie",verified.Headers.GetValues("Set-Cookie").Single(c=>c.StartsWith("ARGOS.Session=")).Split(';')[0]);
        var profile=await host.Client.GetAsync("/api/session/profile");
        Assert.Equal(HttpStatusCode.OK,profile.StatusCode);
        Assert.Contains("personnel.manage",await profile.Content.ReadAsStringAsync());
        Assert.True(profile.Headers.CacheControl!.NoStore);
        host.Adapter.Permitted=false;
        Assert.DoesNotContain("personnel.manage",await host.Client.GetStringAsync("/api/session/profile"));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealCookieHonorsIdleAndAbsoluteLimits(bool interactive)
    {
        var clock = new SessionClock();
        await using var host = await Host.Start(true, false, clock);
        using var begin = await host.Client.PostAsJsonAsync("/api/session/begin", new { Identification = "test", Password = "test" });
        var pending = (await begin.Content.ReadFromJsonAsync<LoginChallenge>())!;
        using var verified = await host.Client.PostAsJsonAsync("/api/session/verify", new { pending.Id, pending.Binding, Code = host.Adapter.Code });
        verified.EnsureSuccessStatusCode();
        host.Client.DefaultRequestHeaders.Add("Cookie", verified.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("ARGOS.Session=")).Split(';')[0]);
        if (interactive)
        {
            for (var i = 0; i < 16; i++)
            {
                clock.Advance(TimeSpan.FromMinutes(29));
                using var activity = await host.Client.PostAsync("/api/session/activity", null);
                Assert.Equal(HttpStatusCode.OK, activity.StatusCode);
            }
            clock.Advance(TimeSpan.FromMinutes(16)); // Exactly eight hours, despite recent activity.
        }
        else
        {
            clock.Advance(TimeSpan.FromMinutes(29));
            using var status = await host.Client.GetAsync("/api/session/status");
            Assert.Equal(HttpStatusCode.OK, status.StatusCode); // Cookie survives the former 20-minute limit.
            using var ordinary = await host.Client.GetAsync("/protected");
            Assert.Equal(HttpStatusCode.OK, ordinary.StatusCode);
            clock.Advance(TimeSpan.FromMinutes(1)); // Neither GET extends inactivity.
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/session/status")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.PostAsync("/api/session/activity", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/protected")).StatusCode);
    }

    private sealed class SessionClock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan time) => now += time;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HttpRequiresHttpsAndProviderFailureNeverIssuesCookie(bool simulateHttps)
    {
        await using var host = await Host.Start(simulateHttps, failDelivery: true);
        using var response = await host.Client.PostAsJsonAsync("/api/session/begin", new { Identification = "test", Password = "test" });
        Assert.Equal(simulateHttps ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain(response.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies : [],
            c => c.StartsWith("ARGOS.Session=") && !c.StartsWith("ARGOS.Session=;"));
    }

    [Fact]
    public async Task MfaCookiePermissionsAreRevalidatedAndLogoutClearsCookie()
    {
        await using var host = await Host.Start(true, false);
        var challenge = await host.Client.PostAsJsonAsync("/api/session/begin", new { Identification = "test", Password = "test" });
        challenge.EnsureSuccessStatusCode();
        var pending = (await challenge.Content.ReadFromJsonAsync<LoginChallenge>())!;
        using var verified = await host.Client.PostAsJsonAsync("/api/session/verify",
            new { pending.Id, pending.Binding, Code = host.Adapter.Code });
        verified.EnsureSuccessStatusCode();
        var cookie = verified.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("ARGOS.Session="));
        Assert.Contains("secure", cookie.ToLowerInvariant());
        Assert.Contains("httponly", cookie.ToLowerInvariant());
        // The test transport is loopback HTTP with a test-only HTTPS scheme. Manually
        // carry the secure cookie to exercise server auth; this does not test real TLS.
        host.Client.DefaultRequestHeaders.Add("Cookie", cookie.Split(';')[0]);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/protected")).StatusCode);
        host.Adapter.Permitted = false;
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync("/protected")).StatusCode);
        host.Adapter.Active = false;
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/protected")).StatusCode);
        using var logout = await host.Client.PostAsync("/api/session/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Contains(logout.Headers.GetValues("Set-Cookie"), c => c.StartsWith("ARGOS.Session=;"));
    }

    [Fact]
    public async Task RecoveryEndpointsResetWithoutSigningInAndRevokeOldCookie()
    {
        await using var host=await Host.Start(true,false);
        var begin=await host.Client.PostAsJsonAsync("/api/session/begin",new { Identification="test",Password="test" });
        var login=(await begin.Content.ReadFromJsonAsync<LoginChallenge>())!;
        var verified=await host.Client.PostAsJsonAsync("/api/session/verify",new {login.Id,login.Binding,Code=host.Adapter.Code});
        host.Client.DefaultRequestHeaders.Add("Cookie",verified.Headers.GetValues("Set-Cookie").Single(c=>c.StartsWith("ARGOS.Session=")).Split(';')[0]);
        var issued=await host.Client.PostAsJsonAsync("/api/session/recovery/begin",new {Identification="test"});
        var recovery=(await issued.Content.ReadFromJsonAsync<RecoveryChallenge>())!;
        var completed=await host.Client.PostAsJsonAsync("/api/session/recovery/complete",new {recovery.Id,recovery.Binding,Code=host.Adapter.Code,NewPassword="New-password-1"});
        Assert.Equal(HttpStatusCode.NoContent,completed.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await host.Client.GetAsync("/protected")).StatusCode);
        var replay=await host.Client.PostAsJsonAsync("/api/session/recovery/complete",new {recovery.Id,recovery.Binding,Code=host.Adapter.Code,NewPassword="New-password-1"});
        Assert.Equal(HttpStatusCode.Unauthorized,replay.StatusCode);
    }
    [Fact]
    public async Task CredentialRotationRejectsExistingCookieEvenWithoutLocalRevocation()
    {
        await using var host=await Host.Start(true,false);
        var begin=await host.Client.PostAsJsonAsync("/api/session/begin",new { Identification="test",Password="test" });
        var login=(await begin.Content.ReadFromJsonAsync<LoginChallenge>())!;
        var verified=await host.Client.PostAsJsonAsync("/api/session/verify",new {login.Id,login.Binding,Code=host.Adapter.Code});
        host.Client.DefaultRequestHeaders.Add("Cookie",verified.Headers.GetValues("Set-Cookie").Single(c=>c.StartsWith("ARGOS.Session=")).Split(';')[0]);
        host.Adapter.Version="changed";
        Assert.Equal(HttpStatusCode.Unauthorized,(await host.Client.GetAsync("/protected")).StatusCode);
    }
    private sealed class Adapter(bool failDelivery) : ILoginAccounts, IVerificationDelivery, IPasswordRecoveryAccounts
    {
        private Guid person = Guid.NewGuid();
        public Guid PersonId { get => person; set => person = value; }
        private readonly Guid organization = Guid.NewGuid();
        public Guid OrganizationId => organization;
        public bool Active { get; set; } = true;
        public bool Permitted { get; set; } = true;
        public bool FailDelivery { get; set; } = failDelivery;
        public string[] Permissions { get; set; } = ["personnel.manage"];
        public string Code { get; private set; } = "";
        public string Version { get; set; } = "initial";
        public Task<RecoveryTarget?> FindAsync(string identification,CancellationToken ct) => Task.FromResult<RecoveryTarget?>(identification=="test"&&Active?new(person,Version):null);
        public Task<bool> ResetAsync(RecoveryTarget target,string password,Guid verification,CancellationToken ct)
        { if(!Active||target.Version!=Version)return Task.FromResult(false); Version=Guid.NewGuid().ToString(); return Task.FromResult(true); }
        public Task<LoginAccount?> GetActiveAsync(Guid personId, CancellationToken ct) => Task.FromResult<LoginAccount?>(
            Active && personId == person ? new(person, organization, "Test", "***1234", VerificationChannel.Sms,
                Permitted ? Permissions : [], CredentialVersion:Version) : null);
        public Task<LoginAccount?> ValidatePasswordAsync(string identification, string password, CancellationToken ct)
            => identification == "test" && password == "test" ? GetActiveAsync(person, ct) : Task.FromResult<LoginAccount?>(null);
        public Task SendAsync(Guid personId, VerificationChannel channel, string code, CancellationToken ct)
        {
            if (FailDelivery) throw new InvalidOperationException("Simulated provider unavailable");
            Code = code;
            return Task.CompletedTask;
        }
    }

    private sealed class Host(WebApplication app, HttpClient client, Adapter adapter) : IAsyncDisposable
    {
        public HttpClient Client { get; } = client;
        public Adapter Adapter { get; } = adapter;
        public IServiceProvider Services => app.Services;
        public static async Task<Host> Start(bool simulateHttps, bool failDelivery, TimeProvider? clock = null, string? casesDatabase = null, IEnvelopeEncryptionProvider? encryption = null)
        {
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            var adapter = new Adapter(failDelivery);
            builder.Services.AddSingleton<ILoginAccounts>(adapter);
            builder.Services.AddSingleton<IVerificationDelivery>(adapter);
            builder.Services.AddSingleton<IPasswordRecoveryAccounts>(adapter);
            builder.Services.AddPersonnelSecurity();
            if (encryption is not null) builder.Services.AddSingleton<IEnvelopeEncryptionProvider>(encryption);
            if (casesDatabase is not null)
            {
                builder.Services.AddDbContext<ARGOS.Infrastructure.Persistence.ArgosDbContext>(options =>
                    Microsoft.EntityFrameworkCore.NpgsqlDbContextOptionsBuilderExtensions.UseNpgsql(options, casesDatabase));
                builder.Services.AddDbContext<PersonnelDbContext>(options => options.UseNpgsql(casesDatabase));
                builder.Services.AddDbContext<AccessDbContext>(options => options.UseNpgsql(casesDatabase));
                builder.Services.AddDbContext<DesignationDbContext>(options => options.UseNpgsql(casesDatabase));
                builder.Services.AddScoped<ARGOS.Application.Common.Persistence.IProcedureRepository, ARGOS.Infrastructure.Persistence.PostgresProcedureRepository>();
                builder.Services.AddScoped<ARGOS.Application.Modules.Procedures.ProcedureService>();
                builder.Services.AddScoped<ARGOS.Application.Common.Persistence.IOrganizationSetupRepository, ARGOS.Infrastructure.Persistence.PostgresOrganizationSetupRepository>();
                builder.Services.AddScoped<ARGOS.Application.Modules.OrganizationSetup.OrganizationSetupService>();
            }
            if (clock is not null)
            {
                builder.Services.AddSingleton<TimeProvider>(clock);
                builder.Services.Configure<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions>(
                    Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme,
                    options => options.TimeProvider = clock);
            }
            builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
            var app = builder.Build();
            if (simulateHttps) app.Use(async (context, next) => { context.Request.Scheme = "https"; await next(context); });
            app.UseAuthentication();
            app.UseMiddleware<ARGOS.API.InitialPasswordGate>();
            app.UseAuthorization();
            app.UseRateLimiter();
            app.MapLoginEndpoints();
            if (casesDatabase is not null)
            {
                app.MapCaseFileEndpoints();
                app.MapProcedureEndpoints();
                app.MapInstitutionalPolicyEndpoints();
                app.MapOrganizationSetupEndpoints();
            }
            app.MapGet("/protected", () => "ok").RequireAuthorization("PersonnelAdministration");
            await app.StartAsync();
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            return new Host(app, new HttpClient(new HttpClientHandler { UseCookies = false }) { BaseAddress = new Uri(address) }, adapter);
        }
        public async ValueTask DisposeAsync() { Client.Dispose(); await app.StopAsync(); await app.DisposeAsync(); }
    }
}
