using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ARGOS.Application.Security;

namespace ARGOS.Infrastructure.Persistence;

/// <summary>OpenBao Transit adapter. OpenBao receives plaintext only for the requested operation and never stores it.</summary>
public sealed class OpenBaoTransitEncryptionProvider(HttpClient client, string keyName, Func<CancellationToken, Task<string>> tokenProvider)
    : IEnvelopeEncryptionProvider
{
    public async Task<EncryptedEnvelope> EncryptAsync(ReadOnlyMemory<byte> plaintext, string associatedData, CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Post, $"v1/transit/encrypt/{Uri.EscapeDataString(keyName)}",
            new EncryptRequest(Convert.ToBase64String(plaintext.ToArray()),
                Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(associatedData)),
                Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(associatedData))), ct);
        var body = await response.Content.ReadFromJsonAsync<TransitResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("OpenBao returned an empty encryption response.");
        var ciphertext = body.Data?.Ciphertext;
        var parts = ciphertext?.Split(':', 3);
        if (parts is not { Length: 3 } || parts[0] != "vault" || !parts[1].StartsWith('v')
            || !int.TryParse(parts[1].AsSpan(1), out var version) || version < 1 || string.IsNullOrEmpty(parts[2]))
            throw new InvalidOperationException("OpenBao returned an invalid encryption response.");
        return new(ciphertext!, parts[1], "openbao-transit", associatedData);
    }

    public async Task<byte[]> DecryptAsync(EncryptedEnvelope envelope, CancellationToken ct = default)
    {
        if (!string.Equals(envelope.Algorithm, "openbao-transit", StringComparison.Ordinal))
            throw new ArgumentException("Envelope algorithm is not supported by OpenBao Transit.", nameof(envelope));
        using var response = await SendAsync(HttpMethod.Post, $"v1/transit/decrypt/{Uri.EscapeDataString(keyName)}",
            new DecryptRequest(envelope.Ciphertext,
                Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(envelope.AssociatedData)),
                Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(envelope.AssociatedData))), ct);
        var body = await response.Content.ReadFromJsonAsync<TransitResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("OpenBao returned an empty decryption response.");
        var plaintext = body.Data?.Plaintext ?? throw new InvalidOperationException("OpenBao returned no plaintext.");
        try { return Convert.FromBase64String(plaintext); }
        catch (FormatException) { throw new InvalidOperationException("OpenBao returned an invalid decryption response."); }
    }

    private async Task<HttpResponseMessage> SendAsync<T>(HttpMethod method, string path, T request, CancellationToken ct)
    {
        using var message = new HttpRequestMessage(method, path) { Content = JsonContent.Create(request) };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokenProvider(ct));
        var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            response.Dispose();
            throw new InvalidOperationException("OpenBao encryption operation failed.");
        }
        return response;
    }

    private sealed record EncryptRequest([property: JsonPropertyName("plaintext")] string Plaintext,
        [property: JsonPropertyName("context")] string Context,
        [property: JsonPropertyName("associated_data")] string AssociatedData);
    private sealed record DecryptRequest([property: JsonPropertyName("ciphertext")] string Ciphertext,
        [property: JsonPropertyName("context")] string Context,
        [property: JsonPropertyName("associated_data")] string AssociatedData);
    private sealed record TransitResponse([property: JsonPropertyName("data")] TransitData? Data);
    private sealed record TransitData([property: JsonPropertyName("ciphertext")] string? Ciphertext,
        [property: JsonPropertyName("plaintext")] string? Plaintext);
}
