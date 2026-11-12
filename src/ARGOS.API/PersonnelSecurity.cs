using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using ARGOS.Application.Security;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ARGOS.API;

public static class PersonnelSecurity
{
    public static IServiceCollection AddPersonnelSecurity(this IServiceCollection services)
    {
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.TryAddSingleton<ILoginAccounts, UnconfiguredLoginAccounts>();
        services.TryAddSingleton<IPasswordChanger, UnconfiguredPasswordChanger>();
        services.TryAddSingleton<IVerificationDelivery, UnconfiguredVerificationDelivery>();
        services.AddSingleton<LoginService>();
        services.TryAddSingleton<IPasswordRecoveryAccounts, UnconfiguredRecoveryAccounts>();
        services.AddSingleton<PasswordRecovery>();
        services.AddSingleton<ActiveSessions>();
        services.AddSingleton<CaseActionVerification>();
        services.AddSingleton<OperationVerification>();
        services.AddScoped<InstitutionalAuthorityResolver>();
        services.AddScoped<EvidenceUseAuthorization>();
        services.AddScoped<ProcedureDecisionGuard>();
        services.AddScoped<IProcedureDecisionGuard>(sp => sp.GetRequiredService<ProcedureDecisionGuard>());
        services.TryAddSingleton<IAuthorityApprovalDelivery, UnconfiguredAuthorityApprovalDelivery>();
        services.TryAddSingleton<IEnvelopeEncryptionProvider, UnconfiguredEnvelopeEncryptionProvider>();
        services.AddSingleton<SuperuserAuthorizationService>();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;
            options.AddFixedWindowLimiter("login", limiter =>
            {
                limiter.PermitLimit = 30;
                limiter.Window = TimeSpan.FromMinutes(1);
                limiter.QueueLimit = 0;
                limiter.AutoReplenishment = true;
            });
        });
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
        {
            options.Cookie.Name = "ARGOS.Session";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = false;
            options.Events.OnValidatePrincipal = async context =>
            {
                var directory = context.HttpContext.RequestServices.GetRequiredService<ILoginAccounts>();
                if (!Guid.TryParse(context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var personId))
                { context.RejectPrincipal(); return; }
                if (!context.HttpContext.Request.IsHttps
                    || !Guid.TryParse(context.Principal?.FindFirstValue("session_id"), out var sessionId)
                    || !context.HttpContext.RequestServices.GetRequiredService<ActiveSessions>().Contains(sessionId, personId))
                { context.RejectPrincipal(); return; }
                var account = await directory.GetActiveAsync(personId, context.HttpContext.RequestAborted);
                if (account is null || account.OrganizationId.ToString() != context.Principal?.FindFirstValue("organization_id"))
                { context.RejectPrincipal(); return; }
                var identity = (ClaimsIdentity)context.Principal!.Identity!;
                if ((identity.FindFirst("credential_version")?.Value ?? "") != account.CredentialVersion)
                { context.RejectPrincipal(); return; }
                if (identity.FindFirst("must_change_password")?.Value != (account.MustChangePassword ? "true" : "false"))
                { context.RejectPrincipal(); return; }
                foreach (var claim in identity.FindAll("permission").ToArray()) identity.RemoveClaim(claim);
                if (!account.MustChangePassword)
                    identity.AddClaims(account.Permissions.Select(p => new Claim("permission", p)));
            };
            options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
            options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
        });
        services.AddAuthorization(options => {
            options.AddPolicy("ProcedureAdministration", policy => policy.RequireAuthenticatedUser()
                .RequireClaim("amr", "mfa").RequireClaim("permission", "procedures.manage")
                .RequireClaim("organization_id").RequireClaim(ClaimTypes.NameIdentifier));
            foreach (var permission in new[] { "case-files.read", "case-files.create", "case-files.update",
                "case-files.finalize", "case-files.archive", "evidences.read", "evidences.create" })
                options.AddPolicy(permission, policy => policy.RequireAuthenticatedUser().RequireClaim("amr", "mfa")
                    .RequireClaim("permission", permission));
            options.AddPolicy("PersonnelAdministration", policy =>
            policy.RequireAuthenticatedUser()
                .RequireClaim(ClaimTypes.NameIdentifier)
                .RequireClaim("amr", "mfa")
                .RequireClaim("permission", "personnel.manage"));
        });
        return services;
    }
}
