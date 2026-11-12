using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ARGOS.Application.Security;

namespace ARGOS.Infrastructure.Persistence;

/// <summary>Cosmian KMS JOSE adapter. The JWE returned by the KMS is stored as the envelope ciphertext.</summary>
public sealed class CosmianKmsEncryptionProvider(HttpClient client, string keyId, Func<CancellationToken, Task<string>> tokenProvider)
    : IEnvelopeEncryptionProvider
{
    public async Task<EncryptedEnvelope> EncryptAsync(ReadOnlyMemory<byte> plaintext, string associatedData, CancellationToken ct = default)
    {
        var request = new EncryptRequest(keyId, "dir", "A256GCM", Base64Url(plaintext.Span), Base64Url(System.Text.Encoding.UTF8.GetBytes(associatedData)));
        using var response = await SendAsync("v1/crypto/encrypt", request, ct);
        var jwe = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        if (jwe.ValueKind != JsonValueKind.Object || !jwe.TryGetProperty("ciphertext", out _)) throw new InvalidOperationException("Cosmian KMS returned an invalid JWE.");
        return new(jwe.GetRawText(), keyId, "cosmian-jose", associatedData);
    }

    public async Task<byte[]> DecryptAsync(EncryptedEnvelope envelope, CancellationToken ct = default)
    {
        if (!string.Equals(envelope.Algorithm, "cosmian-jose", StringComparison.Ordinal))
            throw new ArgumentException("Envelope algorithm is not supported by Cosmian KMS.", nameof(envelope));
        using var content = new StringContent(envelope.Ciphertext, System.Text.Encoding.UTF8, "application/json");
        using var message = new HttpRequestMessage(HttpMethod.Post, "v1/crypto/decrypt") { Content = content };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokenProvider(ct));
        using var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Cosmian KMS decryption failed.");
        var body = await response.Content.ReadFromJsonAsync<DecryptResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Cosmian KMS returned an empty response.");
        return FromBase64Url(body.Data);
    }

    private async Task<HttpResponseMessage> SendAsync(string path, EncryptRequest request, CancellationToken ct)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(request) };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokenProvider(ct));
        var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode) { response.Dispose(); throw new InvalidOperationException("Cosmian KMS encryption failed."); }
        return response;
    }
    private static string Base64Url(ReadOnlySpan<byte> bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static byte[] FromBase64Url(string value) => Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/') + new string('=', (4 - value.Length % 4) % 4));
    private sealed record EncryptRequest([property: JsonPropertyName("kid")] string Kid, [property: JsonPropertyName("alg")] string Alg,
        [property: JsonPropertyName("enc")] string Enc, [property: JsonPropertyName("data")] string Data, [property: JsonPropertyName("aad")] string Aad);
    private sealed record DecryptResponse([property: JsonPropertyName("data")] string Data);
}
