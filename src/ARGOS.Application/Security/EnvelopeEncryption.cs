namespace ARGOS.Application.Security;

public sealed record EncryptedEnvelope(string Ciphertext, string KeyVersion, string Algorithm,
    string AssociatedData);

/// <summary>Boundary for envelope encryption; implementations must keep key material outside ARGOS.</summary>
public interface IEnvelopeEncryptionProvider
{
    Task<EncryptedEnvelope> EncryptAsync(ReadOnlyMemory<byte> plaintext, string associatedData, CancellationToken ct = default);
    Task<byte[]> DecryptAsync(EncryptedEnvelope envelope, CancellationToken ct = default);
}

public sealed class UnconfiguredEnvelopeEncryptionProvider : IEnvelopeEncryptionProvider
{
    public Task<EncryptedEnvelope> EncryptAsync(ReadOnlyMemory<byte> plaintext, string associatedData, CancellationToken ct = default)
        => throw new InvalidOperationException("Envelope encryption provider is not configured.");
    public Task<byte[]> DecryptAsync(EncryptedEnvelope envelope, CancellationToken ct = default)
        => throw new InvalidOperationException("Envelope encryption provider is not configured.");
}
