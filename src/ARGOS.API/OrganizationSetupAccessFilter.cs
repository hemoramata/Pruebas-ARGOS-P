using System.Net;
using System.Security.Claims;
using ARGOS.Infrastructure.Persistence;
using ARGOS.Domain.Aggregates.OrganizationSetup.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace ARGOS.API;

public sealed class OrganizationSetupAccessFilter : IEndpointFilter
{
    private readonly IPAddress? trustedInitialSetupRemote;

    public OrganizationSetupAccessFilter(IConfiguration configuration)
    {
        // A VM accessed through a host-only NAT port-forward sees the host as
        // 10.0.2.2 rather than a loopback address. It is never trusted unless
        // the server operator explicitly configures this single address.
        IPAddress.TryParse(configuration["ARGOS_INITIAL_SETUP_TRUSTED_REMOTE"], out trustedInitialSetupRemote);
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        http.Response.Headers.CacheControl = "no-store";
        var db = http.RequestServices.GetRequiredService<ArgosDbContext>();
        var setup = await db.OrganizationSetups.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Status == OrganizationSetupStatus.Published, http.RequestAborted);
        if (setup is null)
        {
            // First installation is a local operation, before institutional accounts exist.
            if (http.Connection.RemoteIpAddress is not { } remote
                || (!IPAddress.IsLoopback(remote) && !remote.Equals(trustedInitialSetupRemote)))
                return Results.StatusCode(403);
            if (!Uri.TryCreate($"{http.Request.Scheme}://{http.Request.Host}", UriKind.Absolute, out var localUri)
                || !localUri.IsLoopback) return Results.StatusCode(403);
            var origin = http.Request.Headers.Origin.ToString();
            if (!string.IsNullOrEmpty(origin) && (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                || !string.Equals(uri.Authority, http.Request.Host.Value, StringComparison.OrdinalIgnoreCase)
                || uri.Scheme != http.Request.Scheme)) return Results.StatusCode(403);
        }
        else
        {
            // After publication, retain the local first-installation screen only
            // until its first account is created. This avoids redirecting the
            // operator to a login that cannot yet exist.
            var isCurrentRead = HttpMethods.IsGet(http.Request.Method)
                && string.Equals(http.Request.Path.Value?.TrimEnd('/'), "/api/organization-setups/current", StringComparison.OrdinalIgnoreCase);
            var isTrustedLocal = http.Connection.RemoteIpAddress is { } remote
                && (IPAddress.IsLoopback(remote) || remote.Equals(trustedInitialSetupRemote));
            if (isCurrentRead && isTrustedLocal
                && !await http.RequestServices.GetRequiredService<AccessDbContext>().Accounts.AnyAsync(http.RequestAborted))
                return await next(context);
            if (http.User.Identity?.IsAuthenticated != true) return Results.Unauthorized();
            var authorization = http.RequestServices.GetRequiredService<IAuthorizationService>();
            if (!(await authorization.AuthorizeAsync(http.User, null, "PersonnelAdministration")).Succeeded
                || http.User.FindFirstValue("organization_id") != setup.OrganizationId.Value.ToString())
                return Results.StatusCode(403);
        }
        return await next(context);
    }
}
