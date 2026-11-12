using System.Text.Json.Serialization;
using System.Security.Cryptography.X509Certificates;
using ARGOS.API;
using ARGOS.API.Endpoints;
using ARGOS.Application.Modules.OrganizationSetup;
using ARGOS.Application.Modules.Procedures;
using ARGOS.Infrastructure;
using ARGOS.Infrastructure.Persistence;
using ARGOS.Application.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddWindowsService();
using var serverLease = ServerServiceConfiguration.Load(builder.Configuration);

// Linux services do not have Windows DPAPI. Persist the ASP.NET protection keys in
// the service data directory and encrypt them with the same protected HTTPS PFX.
var keyDirectory = builder.Configuration["ARGOS_DATA_PROTECTION_KEYS"];
var certificatePath = builder.Configuration["Kestrel:Endpoints:Https:Certificate:Path"];
var certificatePassword = builder.Configuration["Kestrel:Endpoints:Https:Certificate:Password"];
X509Certificate2? dataProtectionCertificate = null;
if (!OperatingSystem.IsWindows() && !string.IsNullOrWhiteSpace(keyDirectory))
{
    if (string.IsNullOrWhiteSpace(certificatePath) || string.IsNullOrWhiteSpace(certificatePassword))
        throw new InvalidOperationException("ARGOS requiere certificado HTTPS para proteger sus claves de datos en Linux.");
    Directory.CreateDirectory(keyDirectory);
    dataProtectionCertificate = X509CertificateLoader.LoadPkcs12FromFile(certificatePath, certificatePassword,
        X509KeyStorageFlags.EphemeralKeySet);
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(keyDirectory))
        .ProtectKeysWithCertificate(dataProtectionCertificate);
}

// Windows Event Log requires elevated permissions and must not prevent ARGOS from
// answering requests when it runs under a normal institutional service account.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddOpenApi();
builder.Services.AddPersonnelSecurity();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("ARGOS requiere ConnectionStrings:DefaultConnection con PostgreSQL.");
if (!connectionString.StartsWith("Host=", StringComparison.OrdinalIgnoreCase)
    && !connectionString.StartsWith("postgres", StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("ARGOS solo admite PostgreSQL como proveedor de datos.");

builder.Services.AddInfrastructure(connectionString);
builder.Services.AddHttpClient("argos-kms", client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
});
var encryptionProvider = builder.Configuration["ARGOS_ENCRYPTION_PROVIDER"]?.Trim().ToLowerInvariant();
var kmsAddress = builder.Configuration["ARGOS_KMS_URL"];
var kmsKey = builder.Configuration["ARGOS_KMS_KEY"];
var kmsToken = builder.Configuration["ARGOS_KMS_TOKEN"];
if (!string.IsNullOrWhiteSpace(kmsAddress) && !string.IsNullOrWhiteSpace(kmsKey) && !string.IsNullOrWhiteSpace(kmsToken)
    && Uri.TryCreate(kmsAddress, UriKind.Absolute, out var kmsUri)
    && kmsUri.Scheme == Uri.UriSchemeHttps
    && encryptionProvider is "openbao" or "cosmian")
{
    builder.Services.AddSingleton<IEnvelopeEncryptionProvider>(sp =>
    {
        var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("argos-kms");
        http.BaseAddress = new Uri(kmsUri.AbsoluteUri.TrimEnd('/') + "/");
        Func<CancellationToken, Task<string>> token = _ => Task.FromResult(kmsToken!);
        return encryptionProvider == "openbao"
            ? new OpenBaoTransitEncryptionProvider(http, kmsKey!, token)
            : new CosmianKmsEncryptionProvider(http, kmsKey!, token);
    });
}
builder.Services.AddSingleton<ARGOS.Application.Security.IVerificationDelivery>(services =>
    new GmailVerificationDelivery(services.GetRequiredService<IServiceScopeFactory>(),
        () => builder.Configuration["Gmail:Sender"],
        () => builder.Configuration["Gmail:AppPassword"]));
builder.Services.AddScoped<OrganizationSetupService>();
builder.Services.AddScoped<ProcedureService>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ArgosDbContext>();
    if (dbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) != true
        || !await dbContext.Database.CanConnectAsync())
        throw new InvalidOperationException("ARGOS no puede iniciar sin conexión PostgreSQL disponible.");

    // A new institutional server starts with an empty PostgreSQL database. Apply
    // the versioned schemas before exposing login or setup, never by recreating
    // an existing database.
    await dbContext.Database.MigrateAsync();
    await PersonnelSchema.InitializeAsync(scope.ServiceProvider.GetRequiredService<PersonnelDbContext>());
    await scope.ServiceProvider.GetRequiredService<AccessDbContext>().InitializeSchemaAsync();
    await scope.ServiceProvider.GetRequiredService<DesignationDbContext>().InitializeSchemaAsync();
}

if (args.Contains("--enroll-administrator"))
{
    await AdministratorEnrollment.RunAsync(app.Services, builder.Configuration, connectionString);
    return;
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseDefaultFiles();
app.UseAuthentication();
app.UseMiddleware<ARGOS.API.InitialPasswordGate>();
app.UseRateLimiter();
app.UseAuthorization();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        if (context.Context.Request.Path.StartsWithSegments("/login") || context.Context.Request.Path.StartsWithSegments("/home")
            || context.Context.Request.Path.StartsWithSegments("/setup"))
            context.Context.Response.Headers.CacheControl = "no-store";
    }
});

app.MapGet("/", () => Results.Redirect("/home/"));

app.MapGet("/health", () => Results.Ok(new
{
    Status = "Healthy",
    Service = "ARGOS.API"
}))
.WithName("Health");

app.MapProcedureEndpoints();
app.MapInstitutionalPolicyEndpoints();
app.MapCaseFileEndpoints();
app.MapOrganizationSetupEndpoints();
app.MapInitialAdministratorEndpoints();
app.MapPersonnelEndpoints();
app.MapAccessAdministrationEndpoints();
app.MapDocumentCategoryEndpoints();
app.MapAuditEndpoints();
app.MapSuperuserEndpoints();
app.MapLoginEndpoints();

app.Run();

public partial class Program;
