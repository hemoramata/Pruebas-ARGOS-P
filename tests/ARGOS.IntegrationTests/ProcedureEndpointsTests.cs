using System.Net;
using System.Net.Http.Json;
using ARGOS.API.Endpoints;
using ARGOS.Application.Modules.Procedures;
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

public sealed class ProcedureEndpointsTests
{
    [Fact]
    public async Task CreateThenGet_ReturnsStoredDraft()
    {
        await using var application = await TestApplication.StartAsync();

        var created = await CreateProcedureAsync(application, "P-001");
        var stored = await application.Client.GetFromJsonAsync<ProcedureDto>(
            $"/api/procedures/{created.Id}");

        Assert.NotNull(stored);
        Assert.Equal(created.Id, stored.Id);
        Assert.Equal(created.DefinitionId, stored.DefinitionId);
        Assert.Equal(created.Code, stored.Code);
        Assert.Equal("Draft", stored?.Status);
        Assert.Empty(stored?.Decisions ?? []);
    }

    [Fact]
    public async Task Create_WithDuplicateCode_ReturnsConflict()
    {
        await using var application = await TestApplication.StartAsync();
        var request = new
        {
            Code = "P-001",
            Name = "Solicitud de constancia",
            Description = "Emisión de una constancia"
        };

        var firstResponse = await application.Client.PostAsJsonAsync(
            "/api/procedures/",
            request);
        var duplicateResponse = await application.Client.PostAsJsonAsync(
            "/api/procedures/",
            request);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
    }

    [Fact]
    public async Task PublishedProcedureAndDecisions_RemainAfterRestart()
    {
        var databasePath = TestApplication.CreateDatabasePath();
        Guid procedureId;

        try
        {
            await using (var firstApplication =
                await TestApplication.StartAsync(databasePath, deleteDatabase: false))
            {
                var created = await CreateProcedureAsync(firstApplication, "P-002");
                var published = await CompleteApprovalAsync(firstApplication, created);
                procedureId = published.Id;
            }

            await using var secondApplication =
                await TestApplication.StartAsync(databasePath, deleteDatabase: false);

            var stored = await secondApplication.Client.GetFromJsonAsync<ProcedureDto>(
                $"/api/procedures/{procedureId}");

            Assert.NotNull(stored);
            Assert.Equal("Published", stored.Status);
            Assert.Equal(3, stored.Decisions.Count);
            Assert.Contains(
                stored.Decisions,
                decision => decision.AuthorizationReference.StartsWith("policy:") && decision.AuthorizationReference.Contains(";mfa:"));
        }
        finally
        {
            await DeleteDatabaseAsync(databasePath);
        }
    }

    [Fact]
    public async Task OperationalList_ExcludesDraftUntilPublication()
    {
        await using var application = await TestApplication.StartAsync();
        var draft = await CreateProcedureAsync(application, "P-LIST");

        var beforePublication = await application.Client
            .GetFromJsonAsync<ProcedureDto[]>("/api/procedures/");
        Assert.Empty(beforePublication ?? []);

        var published = await CompleteApprovalAsync(application, draft);
        var afterPublication = await application.Client
            .GetFromJsonAsync<ProcedureDto[]>("/api/procedures/");

        Assert.Equal(published.Id, Assert.Single(afterPublication ?? []).Id);
    }

    [Fact]
    public async Task NewVersion_DoesNotReplacePublishedVersionUntilItsOwnPublication()
    {
        await using var application = await TestApplication.StartAsync();
        var originalDraft = await CreateProcedureAsync(application, "P-003");
        var original = await CompleteApprovalAsync(application, originalDraft);

        var versionResponse = await application.Client.PostAsJsonAsync(
            $"/api/procedures/{original.Id}/versions",
            new
            {
                Name = "Nombre actualizado",
                Description = "Descripción actualizada"
            });
        var revision = await ReadRequiredAsync(versionResponse);

        Assert.Equal(HttpStatusCode.Created, versionResponse.StatusCode);
        Assert.Equal("Draft", revision.Status);
        Assert.Equal(original.Id, revision.PreviousVersionId);

        await AssertPublishedAsync(application, original.Id);

        await SubmitAsync(application, revision.Id, "redactor-v2", requiredApprovals: 2);
        var afterFirstApproval = await DecideAsync(
            application,
            revision.Id,
            "approve",
            "aprobador-v2-a");
        Assert.Equal("InReview", afterFirstApproval.Status);
        await AssertPublishedAsync(application, original.Id);

        var approved = await DecideAsync(
            application,
            revision.Id,
            "approve",
            "aprobador-v2-b");
        Assert.Equal("Approved", approved.Status);
        await AssertPublishedAsync(application, original.Id);

        var publishedRevision = await DecideAsync(
            application,
            revision.Id,
            "publish",
            "publicador-v2");
        Assert.Equal("Published", publishedRevision.Status);
        await AssertPublishedAsync(application, revision.Id);

        var storedOriginal = await application.Client.GetFromJsonAsync<ProcedureDto>(
            $"/api/procedures/{original.Id}");
        Assert.Equal("Retired", storedOriginal?.Status);

        var versions = await application.Client.GetFromJsonAsync<ProcedureDto[]>(
            $"/api/procedures/{revision.Id}/versions");
        Assert.Collection(
            versions ?? [],
            first => Assert.Equal(1, first.Version),
            second => Assert.Equal(2, second.Version));
    }

    [Fact]
    public async Task Submitter_CannotApproveWhenSeparationOfDutiesIsRequired()
    {
        await using var application = await TestApplication.StartAsync();
        var draft = await CreateProcedureAsync(application, "P-SEG");
        await SubmitAsync(application, draft.Id, "misma-persona");

        var response = await application.Client.PostAsJsonAsync(
            $"/api/procedures/{draft.Id}/approve",
            Decision("misma-persona"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var stored = await application.Client.GetFromJsonAsync<ProcedureDto>(
            $"/api/procedures/{draft.Id}");
        Assert.Equal("InReview", stored?.Status);
        Assert.Single(stored?.Decisions ?? []);
    }

    [Fact]
    public async Task RejectedRevision_RemainsInHistoryAndCanLeadToANewDraft()
    {
        await using var application = await TestApplication.StartAsync();
        var first = await CompleteApprovalAsync(
            application,
            await CreateProcedureAsync(application, "P-REJ"));

        var version2Response = await application.Client.PostAsJsonAsync(
            $"/api/procedures/{first.Id}/versions",
            new { Name = "Versión 2", Description = "Revisión rechazada" });
        var version2 = await ReadRequiredAsync(version2Response);
        await SubmitAsync(application, version2.Id, "redactor-v2");
        var rejected = await DecideAsync(
            application,
            version2.Id,
            "reject",
            "revisor-v2");

        var version3Response = await application.Client.PostAsJsonAsync(
            $"/api/procedures/{version2.Id}/versions",
            new { Name = "Versión 3", Description = "Revisión corregida" });
        var version3 = await ReadRequiredAsync(version3Response);

        Assert.Equal("Rejected", rejected.Status);
        Assert.Equal(HttpStatusCode.Created, version3Response.StatusCode);
        Assert.Equal(3, version3.Version);
        Assert.Equal(version2.Id, version3.PreviousVersionId);
        await AssertPublishedAsync(application, first.Id);
    }

    [Fact]
    public async Task EmergencyPublication_RequiresADeadlineAndLaterRatification()
    {
        await using var application = await TestApplication.StartAsync();
        var draft = await CreateProcedureAsync(application, "P-EMERGENCY");

        var submitResponse = await application.Client.PostAsJsonAsync(
            $"/api/procedures/{draft.Id}/submit-review",
            new
            {
                Policy = Policy(requiredApprovals: 2, emergencyPublicationAllowed: true),
                Decision = Decision("redactor-emergencia")
            });
        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);

        var emergencyResponse = await application.Client.PostAsJsonAsync(
            $"/api/procedures/{draft.Id}/publish-emergency",
            new
            {
                RatificationDueAtUtc = DateTime.UtcNow.AddHours(4),
                Decision = Decision("publicador-emergencia")
            });
        var emergencyPublished = await ReadRequiredAsync(emergencyResponse);

        Assert.Equal(HttpStatusCode.OK, emergencyResponse.StatusCode);
        Assert.Equal("Published", emergencyPublished.Status);
        Assert.True(emergencyPublished.EmergencyRatificationPending);
        Assert.NotNull(emergencyPublished.EmergencyRatificationDueAtUtc);

        var ratified = await DecideAsync(
            application,
            draft.Id,
            "ratify-emergency",
            "ratificador-emergencia");

        Assert.False(ratified.EmergencyRatificationPending);
        Assert.Contains(
            ratified.Decisions,
            decision => decision.Type == "EmergencyRatified");
    }

    [Fact]
    public async Task InvalidDecisionMetadata_ReturnsBadRequest()
    {
        await using var application = await TestApplication.StartAsync();
        var draft = await CreateProcedureAsync(application, "P-INVALID");

        var response = await application.Client.PostAsJsonAsync(
            $"/api/procedures/{draft.Id}/submit-review",
            new
            {
                Policy = Policy(),
                Decision = Decision("")
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task InvalidOrMissingApprovalPolicy_ReturnsBadRequestWithoutChangingDraft()
    {
        await using var application = await TestApplication.StartAsync();
        var draft = await CreateProcedureAsync(application, "P-POLICY");

        var invalidResponse = await application.Client.PostAsJsonAsync(
            $"/api/procedures/{draft.Id}/submit-review",
            new
            {
                Policy = Policy(requiredApprovals: 0),
                Decision = Decision("redactor")
            });
        var missingResponse = await application.Client.PostAsJsonAsync(
            $"/api/procedures/{draft.Id}/submit-review",
            new
            {
                Policy = (object?)null,
                Decision = Decision("redactor")
            });

        Assert.Equal(HttpStatusCode.Forbidden, invalidResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, missingResponse.StatusCode);

        var stored = await application.Client.GetFromJsonAsync<ProcedureDto>(
            $"/api/procedures/{draft.Id}");
        Assert.Equal("Draft", stored?.Status);
        Assert.Empty(stored?.Decisions ?? []);
    }

    private static async Task<ProcedureDto> CreateProcedureAsync(
        TestApplication application,
        string code)
    {
        var response = await application.Client.PostAsJsonAsync(
            "/api/procedures/",
            new
            {
                Code = code,
                Name = "Solicitud de constancia",
                Description = "Emisión de una constancia"
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadRequiredAsync(response);
    }

    private static async Task<ProcedureDto> CompleteApprovalAsync(
        TestApplication application,
        ProcedureDto draft)
    {
        await SubmitAsync(application, draft.Id, "redactor");
        var approved = await DecideAsync(application, draft.Id, "approve", "aprobador");
        Assert.Equal("Approved", approved.Status);
        return await DecideAsync(application, draft.Id, "publish", "publicador");
    }

    private static async Task<ProcedureDto> SubmitAsync(
        TestApplication application,
        Guid id,
        string actor,
        int requiredApprovals = 1)
    {
        var response = await application.Client.PostAsJsonAsync(
            $"/api/procedures/{id}/submit-review",
            new
            {
                Policy = Policy(requiredApprovals),
                Decision = Decision(actor)
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadRequiredAsync(response);
    }

    private static async Task<ProcedureDto> DecideAsync(
        TestApplication application,
        Guid id,
        string action,
        string actor)
    {
        var response = await application.Client.PostAsJsonAsync(
            $"/api/procedures/{id}/{action}",
            Decision(actor));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadRequiredAsync(response);
    }

    private static async Task AssertPublishedAsync(
        TestApplication application,
        Guid expectedId)
    {
        var published = await application.Client
            .GetFromJsonAsync<ProcedureDto[]>("/api/procedures/");

        Assert.Equal(expectedId, Assert.Single(published ?? []).Id);
    }

    private static async Task<ProcedureDto> ReadRequiredAsync(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<ProcedureDto>()
            ?? throw new InvalidOperationException("The API did not return a procedure.");

    private static object Policy(
        int requiredApprovals = 1,
        bool emergencyPublicationAllowed = false)
        => new
        {
            RequiredApprovalCount = requiredApprovals,
            SeparationOfDutiesRequired = true,
            EmergencyPublicationAllowed = emergencyPublicationAllowed,
            EmergencyRatificationRequired = emergencyPublicationAllowed
        };

    private static object Decision(string actor)
        => new
        {
            ActorReference = actor,
            AssignmentReference = $"assignment-{actor}",
            PositionReference = $"position-{actor}",
            CompetenceCode = "PROCEDURE-APPROVAL",
            PolicyVersion = "policy-v1",
            AuthorizationReference = $"authorization-{actor}",
            Comments = "Integration test decision"
        };

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
            builder.Services.AddInfrastructure(
                databasePath);
            builder.Services.AddScoped<ProcedureService>();
            ARGOS.API.PersonnelSecurity.AddPersonnelSecurity(builder.Services);
            var clock = new ProcedureTestClock();
            builder.Services.AddSingleton<TimeProvider>(clock);
            builder.Services.AddSingleton<ProcedureTestDelivery>();
            builder.Services.AddSingleton<ARGOS.Application.Security.ILoginAccounts>(sp => sp.GetRequiredService<ProcedureTestDelivery>());
            builder.Services.AddSingleton<ARGOS.Application.Security.IVerificationDelivery>(sp => sp.GetRequiredService<ProcedureTestDelivery>());

            var application = builder.Build();

            await using (var scope = application.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider
                    .GetRequiredService<ArgosDbContext>();
                await TestPostgresDatabase.InitializeAsync(databasePath);
            }

            application.UseDeveloperExceptionPage();
            application.UseTestAdministrator();
            application.UseRateLimiter();
            application.MapProcedureEndpoints();
            await application.StartAsync();

            var server = application.Services.GetRequiredService<IServer>();
            var addresses = server.Features.Get<IServerAddressesFeature>();
            var address = addresses?.Addresses.Single()
                ?? throw new InvalidOperationException("No HTTP address was assigned.");

            var client = new HttpClient(new ProcedureSecurityTestClient(application.Services,
                application.Services.GetRequiredService<ProcedureTestDelivery>(), clock)) { BaseAddress = new Uri(address) };
            return new TestApplication(
                application,
                client,
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
