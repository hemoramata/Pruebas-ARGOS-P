using System.Net;
using System.Net.Http.Json;
using ARGOS.API.Endpoints;
using ARGOS.Application.Modules.OrganizationSetup;
using ARGOS.Domain.Aggregates.OrganizationSetup.Enums;
using ARGOS.Infrastructure;
using ARGOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Npgsql;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ARGOS.IntegrationTests;

public sealed class OrganizationSetupEndpointsTests
{
    [Fact]
    public async Task CompleteConfiguration_RemainsAfterApplicationRestart()
    {
        var databasePath = TestApplication.CreateDatabasePath();
        Guid setupId;

        try
        {
            await using (var firstApplication =
                await TestApplication.StartAsync(databasePath, deleteDatabase: false))
            {
                var configured = await CreateCompleteSetupAsync(firstApplication, "MUNI-01");
                var published = await PublishAsync(firstApplication, configured.Id, "publicador");

                Assert.Equal("Published", published.Status);
                setupId = published.Id;
            }

            await using var restartedApplication =
                await TestApplication.StartAsync(databasePath, deleteDatabase: false);

            var stored = await restartedApplication.Client
                .GetFromJsonAsync<OrganizationSetupDto>(
                    $"/api/organization-setups/{setupId}");

            Assert.NotNull(stored);
            Assert.Equal("MUNI-01", stored.Organization.Code);
            Assert.Equal("Published", stored.Status);
            Assert.Equal(7, stored.Terms.Count);
            Assert.Single(stored.Units);
            Assert.Single(stored.Competences);
            Assert.Single(stored.ActingCapacities);

            var position = Assert.Single(stored.Positions);
            Assert.Equal(Assert.Single(stored.Competences).Id, Assert.Single(position.CompetenceIds));
            Assert.Equal(Assert.Single(stored.ActingCapacities).Id, Assert.Single(position.ActingCapacityIds));
            Assert.NotNull(stored.PublishedAtUtc);
            Assert.Equal("autorizacion-publicador", stored.PublicationAuthorizationReference);

            var current = await restartedApplication.Client
                .GetFromJsonAsync<OrganizationSetupDto>(
                    "/api/organization-setups/current");
            Assert.Equal(setupId, current?.Id);
        }
        finally
        {
            await DeleteDatabaseAsync(databasePath);
        }
    }

    [Fact]
    public async Task SecondOrganization_OnSameInstallation_IsRejected()
    {
        await using var application = await TestApplication.StartAsync();
        await CreateInitialAsync(application, "INSTITUCION-PRINCIPAL");

        var response = await application.Client.PostAsJsonAsync(
            "/api/organization-setups/",
            new
            {
                OrganizationName = "Segunda institución",
                OrganizationCode = "INSTITUCION-SECUNDARIA",
                Description = "No debe crearse en la misma instalación",
                Action = Action("implantador")
            });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("OrganizationSetup.InstallationAlreadyExists", error?.Code);
    }

    [Fact]
    public async Task IncompleteConfiguration_CannotBePublished()
    {
        await using var application = await TestApplication.StartAsync();
        var setup = await CreateInitialAsync(application, "EMPRESA-01");

        var response = await application.Client.PostAsJsonAsync(
            $"/api/organization-setups/{setup.Id}/publish",
            Action("publicador"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var stored = await application.Client.GetFromJsonAsync<OrganizationSetupDto>(
            $"/api/organization-setups/{setup.Id}");
        Assert.Equal("Draft", stored?.Status);
        Assert.Null(stored?.PublishedAtUtc);
    }

    [Fact]
    public async Task NewVersion_PreservesPublishedConfigurationUntilReplacementIsPublished()
    {
        await using var application = await TestApplication.StartAsync();
        var first = await PublishAsync(
            application,
            (await CreateCompleteSetupAsync(application, "INSTITUCION-01")).Id,
            "publicador-v1");

        var versionResponse = await application.Client.PostAsJsonAsync(
            $"/api/organization-setups/{first.Id}/versions",
            Action("configurador-v2"));
        var revision = await ReadRequiredAsync(versionResponse);

        Assert.Equal(HttpStatusCode.Created, versionResponse.StatusCode);
        Assert.Equal(2, revision.Version);
        Assert.Equal("Draft", revision.Status);
        Assert.Equal(first.Id, revision.PreviousVersionId);
        Assert.Equal(first.Units.Single().Id, revision.Units.Single().Id);

        var renamedResponse = await application.Client.PutAsJsonAsync(
            $"/api/organization-setups/{revision.Id}/units/{revision.Units.Single().Id}",
            new { Name = "Dirección General Actualizada" });
        var renamedRevision = await ReadRequiredAsync(renamedResponse);

        Assert.Equal("Dirección General Actualizada", renamedRevision.Units.Single().Name);

        var stillPublished = await application.Client.GetFromJsonAsync<OrganizationSetupDto>(
            $"/api/organization-setups/{first.Id}");
        Assert.Equal("Published", stillPublished?.Status);
        Assert.Equal("Dirección General", stillPublished?.Units.Single().Name);

        var replacement = await PublishAsync(application, revision.Id, "publicador-v2");
        var retired = await application.Client.GetFromJsonAsync<OrganizationSetupDto>(
            $"/api/organization-setups/{first.Id}");

        Assert.Equal("Published", replacement.Status);
        Assert.Equal("Retired", retired?.Status);

        var versions = await application.Client.GetFromJsonAsync<OrganizationSetupDto[]>(
            $"/api/organization-setups/organizations/{first.Organization.Id}/versions");
        Assert.Collection(
            versions ?? [],
            version1 => Assert.Equal("Retired", version1.Status),
            version2 => Assert.Equal("Published", version2.Status));
    }

    private static async Task<OrganizationSetupDto> CreateCompleteSetupAsync(
        TestApplication application,
        string organizationCode)
    {
        var setup = await CreateInitialAsync(application, organizationCode);

        foreach (var concept in Enum.GetValues<OrganizationConcept>())
        {
            var response = await application.Client.PutAsJsonAsync(
                $"/api/organization-setups/{setup.Id}/terms/{concept}",
                new
                {
                    Singular = $"{concept} local",
                    Plural = $"{concept} locales"
                });
            setup = await ReadRequiredAsync(response);
        }

        setup = await PostAndReadAsync(
            application,
            $"/api/organization-setups/{setup.Id}/units",
            new { Code = "DG", Name = "Dirección General", ParentUnitId = (Guid?)null });
        var unitId = setup.Units.Single().Id;

        setup = await PostAndReadAsync(
            application,
            $"/api/organization-setups/{setup.Id}/competences",
            new
            {
                Code = "RESOLVER",
                Name = "Resolver expedientes",
                Description = "Competencia definida por la institución"
            });
        var competenceId = setup.Competences.Single().Id;

        setup = await PostAndReadAsync(
            application,
            $"/api/organization-setups/{setup.Id}/acting-capacities",
            new
            {
                Code = "TITULAR",
                Name = "Titular",
                Description = "Calidad definida por la institución"
            });
        var capacityId = setup.ActingCapacities.Single().Id;

        setup = await PostAndReadAsync(
            application,
            $"/api/organization-setups/{setup.Id}/positions",
            new
            {
                Code = "DIRECTOR",
                Name = "Director general",
                OrganizationUnitId = unitId
            });
        var positionId = setup.Positions.Single().Id;

        setup = await PostAndReadAsync(
            application,
            $"/api/organization-setups/{setup.Id}/positions/{positionId}/competences/{competenceId}",
            new { });

        return await PostAndReadAsync(
            application,
            $"/api/organization-setups/{setup.Id}/positions/{positionId}/acting-capacities/{capacityId}",
            new { });
    }

    private static async Task<OrganizationSetupDto> CreateInitialAsync(
        TestApplication application,
        string organizationCode)
    {
        var response = await application.Client.PostAsJsonAsync(
            "/api/organization-setups/",
            new
            {
                OrganizationName = $"Organización {organizationCode}",
                OrganizationCode = organizationCode,
                Description = "Configuración de integración",
                Action = Action("implantador")
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadRequiredAsync(response);
    }

    private static async Task<OrganizationSetupDto> PublishAsync(
        TestApplication application,
        Guid setupId,
        string actor)
        => await PostAndReadAsync(
            application,
            $"/api/organization-setups/{setupId}/publish",
            Action(actor));

    private static async Task<OrganizationSetupDto> PostAndReadAsync(
        TestApplication application,
        string route,
        object request)
    {
        var response = await application.Client.PostAsJsonAsync(route, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadRequiredAsync(response);
    }

    private static async Task<OrganizationSetupDto> ReadRequiredAsync(
        HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<OrganizationSetupDto>()
            ?? throw new InvalidOperationException(
                $"La API no devolvió una configuración. Estado: {response.StatusCode}.");

    private static object Action(string actor)
        => new
        {
            ActorReference = actor,
            PolicyVersion = "politica-organizacional-v1",
            AuthorizationReference = $"autorizacion-{actor}"
        };

    private sealed record ErrorResponse(string Code, string Description);

    private sealed class TestApplication : IAsyncDisposable
    {
        private readonly WebApplication _application;
        private readonly string _databasePath;
        private readonly bool _deleteDatabase;

        private TestApplication(
            WebApplication application,
            HttpClient client,
            string databasePath,
            bool deleteDatabase)
        {
            _application = application;
            Client = client;
            _databasePath = databasePath;
            _deleteDatabase = deleteDatabase;
        }

        public HttpClient Client { get; }

        public static string CreateDatabasePath() => TestPostgresDatabase.CreateConnectionString();

        public static async Task<TestApplication> StartAsync(
            string? databasePath = null,
            bool deleteDatabase = true)
        {
            databasePath ??= CreateDatabasePath();

            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddInfrastructure(databasePath);
            builder.Services.AddScoped<OrganizationSetupService>();
            ARGOS.API.PersonnelSecurity.AddPersonnelSecurity(builder.Services);

            var application = builder.Build();

            await using (var scope = application.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ArgosDbContext>();
                await TestPostgresDatabase.InitializeAsync(databasePath);
            }

            application.UseTestAdministrator();
            application.MapOrganizationSetupEndpoints();
            await application.StartAsync();

            var server = application.Services.GetRequiredService<IServer>();
            var addresses = server.Features.Get<IServerAddressesFeature>();
            var address = addresses?.Addresses.Single()
                ?? throw new InvalidOperationException("No HTTP address was assigned.");

            return new TestApplication(
                application,
                new HttpClient { BaseAddress = new Uri(address) },
                databasePath,
                deleteDatabase);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _application.StopAsync();
            await _application.DisposeAsync();

            if (_deleteDatabase)
                await DeleteDatabaseAsync(_databasePath);
        }
    }

    private static Task DeleteDatabaseAsync(string databasePath) => TestPostgresDatabase.ReleaseAsync(databasePath);
}
