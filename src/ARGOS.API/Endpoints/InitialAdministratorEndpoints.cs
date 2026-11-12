using System.Net;
using System.Net.Mail;
using ARGOS.Application.Security;
using ARGOS.Domain.Aggregates.Organization.ValueObjects;
using ARGOS.Domain.Aggregates.Personnel;
using ARGOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ARGOS.API.Endpoints;

/// <summary>Bootstraps the only first administrator from a local, empty installation.</summary>
public static class InitialAdministratorEndpoints
{
    public static IEndpointRouteBuilder MapInitialAdministratorEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/initial-administrator", async (InitialAdministratorRequest request, HttpContext http,
            IConfiguration configuration, ArgosDbContext main, AccessDbContext access, CancellationToken ct) =>
        {
            if (!IsLocalFirstSetup(http, configuration)) return Results.StatusCode(403);
            if (await access.Accounts.AnyAsync(ct)) return Results.Conflict(new { code = "InitialAdministrator.AlreadyExists" });
            var setup = await main.OrganizationSetups.AsNoTracking().Include(x => x.Positions)
                .FirstOrDefaultAsync(x => x.Status == ARGOS.Domain.Aggregates.OrganizationSetup.Enums.OrganizationSetupStatus.Published, ct);
            if (setup is null || setup.Positions.Count == 0) return Results.Conflict(new { code = "InitialAdministrator.PublishedSetupRequired" });
            if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.Identification) || string.IsNullOrEmpty(request.Password)
                || !MailAddress.TryCreate(request.Email, out var address) || address.Address != request.Email.Trim())
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = ["Complete nombre, identificación y correo válido."] });
            try { AccessAccount.ValidatePasswordPolicy(request.Password); }
            catch (ArgumentException ex) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["password"] = [ex.Message] }); }

            var now = DateTimeOffset.UtcNow;
            var person = new InstitutionalPerson(setup.OrganizationId,
                new OfficialIdentity("CR", "Identificación", request.Identification.Trim()), request.FullName.Trim());
            var position = setup.Positions.OrderBy(x => x.Code, StringComparer.Ordinal).First();
            var assignment = person.Assign(setup, position.Id, now, null, "Alta inicial local");
            var designation = AdministratorDesignation.Create(person, assignment.Id, "Alta inicial local", request.FullName.Trim(), request.FullName.Trim(), now, now, null);
            var account = AccessAccount.Provision(person.Id, request.Identification.Trim(), request.Password,
                VerificationChannel.InstitutionalEmail, request.Email.Trim(), "email-challenge:local-bootstrap");
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("PostgreSQL no está configurado.");
            try
            {
                await AdministratorEnrollment.SaveAsync(connectionString, person, designation, account);
                return Results.NoContent();
            }
            catch (DbUpdateException error) when (error.InnerException is PostgresException
                { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                return Results.Conflict(new { code = "InitialAdministrator.DuplicateIdentityOrAccount" });
            }
        }).RequireRateLimiting("login");
        return routes;
    }

    private static bool IsLocalFirstSetup(HttpContext http, IConfiguration configuration)
    {
        if (!Uri.TryCreate($"{http.Request.Scheme}://{http.Request.Host}", UriKind.Absolute, out var host) || !host.IsLoopback) return false;
        var remote = http.Connection.RemoteIpAddress;
        if (remote is null) return false;
        if (IPAddress.IsLoopback(remote)) return true;
        return IPAddress.TryParse(configuration["ARGOS_INITIAL_SETUP_TRUSTED_REMOTE"], out var trusted) && remote.Equals(trusted);
    }

    public sealed record InitialAdministratorRequest(string? FullName, string? Identification, string? Email, string? Password);
}
