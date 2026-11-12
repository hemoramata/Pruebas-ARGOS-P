using System.Security.Claims;
using ARGOS.Application.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using ARGOS.Infrastructure.Persistence;

namespace ARGOS.API.Endpoints;

public static class LoginEndpoints
{
    public static IEndpointRouteBuilder MapLoginEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/session").RequireRateLimiting("login");
        group.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            if (!context.HttpContext.Request.IsHttps)
                return Results.BadRequest(new { code = "Session.HttpsRequired" });
            return await next(context);
        });
        group.MapPost("/begin", async (BeginLogin request, LoginService service, HttpContext http, CancellationToken ct) =>
        {
            RevokeSession(http);
            await http.SignOutAsync();
            try
            {
                var challenge = await service.BeginAsync(request.Identification, request.Password, ct);
                return challenge is null ? Results.Unauthorized() : Results.Ok(challenge);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            { return Results.StatusCode(503); }
            catch (InvalidOperationException) { return Results.StatusCode(503); }
        });
        group.MapPost("/verify", async (VerifyLogin request, LoginService service, HttpContext http, CancellationToken ct) =>
        {
            var account = await service.CompleteAsync(request.Id, request.Binding, request.Code, ct);
            if (account is null) return Results.Unauthorized();
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, account.PersonId.ToString()),
                new(ClaimTypes.Name, account.FullName),
                new("organization_id", account.OrganizationId.ToString()),
                new("session_id", request.Binding.ToString()), new("amr", "mfa"),
                new("must_change_password", account.MustChangePassword ? "true" : "false"),
                new("credential_version", account.CredentialVersion)
            };
            var permissions = account.MustChangePassword ? Array.Empty<string>() : account.Permissions;
            claims.AddRange(permissions.Select(p => new Claim("permission", p)));
            http.RequestServices.GetRequiredService<ActiveSessions>().Add(request.Binding, account.PersonId);
            await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
                new AuthenticationProperties { IsPersistent = false });
            return Results.Ok(new { account.FullName, Permissions = permissions, account.MustChangePassword });
        });
        group.MapPost("/password/change", async (ChangePassword request, HttpContext http, IPasswordChanger accounts, CancellationToken ct) =>
        {
            if (!http.User.HasClaim("must_change_password", "true")
                || !Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var personId))
                return Results.Forbid();
            try
            {
                var changed = await accounts.ChangePasswordAsync(personId, request.CurrentPassword, request.NewPassword, ct);
                if (!changed) return Results.Unauthorized();
                http.RequestServices.GetRequiredService<ActiveSessions>().RevokeForPerson(personId);
                await http.SignOutAsync();
                return Results.NoContent();
            }
            catch (ArgumentException ex) { return Results.BadRequest(new { code = "Password.Policy", detail = ex.Message }); }
        }).RequireAuthorization(policy => policy.RequireAuthenticatedUser().RequireClaim("amr", "mfa"));
        group.MapPost("/cancel", async (CancelLogin request, LoginService service, CancellationToken ct) =>
        {
            await service.CancelAsync(request.Id, request.Binding, ct);
            return Results.NoContent();
        });
        group.MapPost("/recovery/begin", async (BeginRecovery request, PasswordRecovery recovery, CancellationToken ct) =>
            Results.Ok(await recovery.BeginAsync(request.Identification, ct)));
        group.MapPost("/recovery/complete", async (CompleteRecovery request, PasswordRecovery recovery, HttpContext http, CancellationToken ct) =>
        {
            try
            {
                AccessAccount.ValidatePasswordPolicy(request.NewPassword);
                if (!await recovery.CompleteAsync(request.Id, request.Binding, request.Code, request.NewPassword, ct)) return Results.Unauthorized();
                RevokeSession(http); await http.SignOutAsync(); return Results.NoContent();
            }
            catch (ArgumentException error) { return Results.BadRequest(new { code="Password.Policy", detail=error.Message }); }
        });
        group.MapPost("/recovery/cancel", async (CancelLogin request, PasswordRecovery recovery, CancellationToken ct) =>
        { await recovery.CancelAsync(request.Id,request.Binding,ct); return Results.NoContent(); });
        group.MapPost("/logout", async (HttpContext http) =>
        {
            RevokeSession(http);
            await http.SignOutAsync();
            return Results.NoContent();
        });
        group.MapGet("/status", (HttpContext http, ActiveSessions sessions) =>
        {
            var remaining = sessions.Remaining(Guid.Parse(http.User.FindFirstValue("session_id")!),
                Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!));
            return remaining is null ? Results.Unauthorized() : Results.Ok(remaining);
        }).RequireAuthorization(policy => policy.RequireAuthenticatedUser().RequireClaim("amr", "mfa"));
        group.MapGet("/profile", (HttpContext http) => Results.Ok(new
        {
            FullName = http.User.FindFirstValue(ClaimTypes.Name),
            Permissions = http.User.FindAll("permission").Select(c => c.Value).Distinct().ToArray(),
            MustChangePassword = http.User.HasClaim("must_change_password", "true")
        })).RequireAuthorization(policy => policy.RequireAuthenticatedUser().RequireClaim("amr", "mfa"));
        group.MapPost("/activity", (HttpContext http, ActiveSessions sessions) =>
        {
            var session = Guid.Parse(http.User.FindFirstValue("session_id")!);
            var person = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            if (!sessions.Touch(session, person)) return Results.Unauthorized();
            return Results.Ok(sessions.Remaining(session, person));
        }).RequireAuthorization(policy => policy.RequireAuthenticatedUser().RequireClaim("amr", "mfa"));
        return routes;
    }

    public sealed record BeginLogin(string Identification, string Password);
    public sealed record BeginRecovery(string Identification);
    public sealed record CompleteRecovery(Guid Id, Guid Binding, string Code, string NewPassword);
    private static void RevokeSession(HttpContext http)
    {
        if (Guid.TryParse(http.User.FindFirstValue("session_id"), out var session))
            http.RequestServices.GetRequiredService<ActiveSessions>().Revoke(session);
    }
    public sealed record VerifyLogin(Guid Id, Guid Binding, string Code);
    public sealed record CancelLogin(Guid Id, Guid Binding);
    public sealed record ChangePassword(string CurrentPassword, string NewPassword);
}
