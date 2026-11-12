using System.Security.Claims;
using ARGOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ARGOS.IntegrationTests;

// Only for lifecycle fixtures. Security tests exercise real login cookies separately.
internal static class TestAdministrativeIdentity
{
    public static void UseTestAdministrator(this WebApplication app)
    {
        app.Use(async (http, next) =>
        {
            var db = http.RequestServices.GetRequiredService<ArgosDbContext>();
            var setup = await db.OrganizationSetups.AsNoTracking().FirstOrDefaultAsync();
            http.User = new ClaimsPrincipal(new ClaimsIdentity(new[] {
                new Claim(ClaimTypes.NameIdentifier, http.Request.Headers.TryGetValue("X-Test-Actor", out var actor) ? actor.ToString() : "test-administrator"),
                new Claim("amr", "mfa"), new Claim("permission", "procedures.manage"),
                new Claim("session_id", http.Request.Headers["X-Test-Session"].ToString()),
                new Claim("permission", "personnel.manage"),
                new Claim("organization_id", (setup?.OrganizationId.Value ?? Guid.NewGuid()).ToString())
            }, "test-only"));
            await next();
        });
        app.UseAuthorization();
    }
}

// The lifecycle test client selects the authenticated actor for each simulated decision.
// This header is understood only by UseTestAdministrator, never by the real API.
internal sealed class TestDecisionActorHandler : DelegatingHandler
{
    public TestDecisionActorHandler() : base(new HttpClientHandler()) { }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (request.Content is not null)
        {
            using var json = System.Text.Json.JsonDocument.Parse(await request.Content.ReadAsStringAsync(ct));
            var data = json.RootElement;
            if (data.TryGetProperty("decision", out var nested)) data = nested;
            if (data.ValueKind == System.Text.Json.JsonValueKind.Object && data.TryGetProperty("actorReference", out var actor))
                request.Headers.TryAddWithoutValidation("X-Test-Actor", actor.GetString() ?? "");
        }
        return await base.SendAsync(request, ct);
    }
}
