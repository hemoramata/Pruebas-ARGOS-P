using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ARGOS.Application.Security;
using ARGOS.Infrastructure.Persistence;

namespace ARGOS.UnitTests;

public sealed class OpenBaoTransitEncryptionTests
{
    [Fact]
    public async Task EncryptAndDecryptUseTransitAndExternalToken()
    {
        var handler = new Handler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://bao.test/") };
        var provider = new OpenBaoTransitEncryptionProvider(http, "argos-case", _ => Task.FromResult("token-1"));
        var encrypted = await provider.EncryptAsync("expediente"u8.ToArray(), "org:1;case:2");
        Assert.Equal("openbao-transit", encrypted.Algorithm);
        Assert.Equal("v1", encrypted.KeyVersion);
        Assert.Equal("token-1", handler.Authorization);
        Assert.Contains("context", handler.LastBody);
        var clear = await provider.DecryptAsync(encrypted);
        Assert.Equal("expediente", System.Text.Encoding.UTF8.GetString(clear));
    }

    [Fact]
    public async Task ProviderFailsClosedOnKmsError()
    {
        var handler = new Handler { Status = HttpStatusCode.Forbidden };
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://bao.test/") };
        var provider = new OpenBaoTransitEncryptionProvider(http, "argos-case", _ => Task.FromResult("token-1"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.EncryptAsync("expediente"u8.ToArray(), "org:1;case:2"));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"data\":null}")]
    [InlineData("{\"data\":{\"ciphertext\":\"invalid\"}}")]
    [InlineData("{\"data\":{\"ciphertext\":\"vault:v0:encoded\"}}")]
    public async Task InvalidEncryptionResponseCannotBecomeStoredEnvelope(string response)
    {
        using var http = new HttpClient(new Handler { ResponseOverride = response }) { BaseAddress = new Uri("https://bao.test/") };
        var provider = new OpenBaoTransitEncryptionProvider(http, "argos-case", _ => Task.FromResult("token"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.EncryptAsync("expediente"u8.ToArray(), "org:1;case:2"));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"data\":null}")]
    [InlineData("{\"data\":{\"plaintext\":\"not-base64!\"}}")]
    public async Task InvalidDecryptionResponseFailsClosed(string response)
    {
        using var http = new HttpClient(new Handler { ResponseOverride = response }) { BaseAddress = new Uri("https://bao.test/") };
        var provider = new OpenBaoTransitEncryptionProvider(http, "argos-case", _ => Task.FromResult("token"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.DecryptAsync(
            new EncryptedEnvelope("vault:v1:encoded", "v1", "openbao-transit", "org:1;case:2")));
    }

    private sealed class Handler : HttpMessageHandler
    {
        public HttpStatusCode Status { get; init; } = HttpStatusCode.OK;
        public string? ResponseOverride { get; init; }
        public string Authorization { get; private set; } = "";
        public string LastBody { get; private set; } = "";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Authorization = request.Headers.Authorization?.Parameter ?? "";
            LastBody = await request.Content!.ReadAsStringAsync(ct);
            var isEncrypt = request.RequestUri!.AbsolutePath.Contains("encrypt", StringComparison.Ordinal);
            using var payload = JsonDocument.Parse(LastBody);
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(isEncrypt ? "/v1/transit/encrypt/argos-case" : "/v1/transit/decrypt/argos-case", request.RequestUri.AbsolutePath);
            Assert.Equal(Convert.ToBase64String("org:1;case:2"u8.ToArray()), payload.RootElement.GetProperty("context").GetString());
            Assert.Equal(Convert.ToBase64String("org:1;case:2"u8.ToArray()), payload.RootElement.GetProperty("associated_data").GetString());
            Assert.False(payload.RootElement.TryGetProperty(isEncrypt ? "ciphertext" : "plaintext", out _));
            Assert.Equal(isEncrypt ? Convert.ToBase64String("expediente"u8.ToArray()) : "vault:v1:encoded",
                payload.RootElement.GetProperty(isEncrypt ? "plaintext" : "ciphertext").GetString());
            var body = isEncrypt
                ? "{\"data\":{\"ciphertext\":\"vault:v1:encoded\"}}"
                : $"{{\"data\":{{\"plaintext\":\"{Convert.ToBase64String("expediente"u8.ToArray())}\"}}}}";
            return new HttpResponseMessage(Status) { Content = new StringContent(ResponseOverride ?? body, System.Text.Encoding.UTF8, "application/json") };
        }
    }
}
