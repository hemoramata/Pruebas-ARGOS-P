using ARGOS.Application.Security;

namespace ARGOS.Infrastructure.Persistence;

public sealed class ProtectedCaseContent
{
    private ProtectedCaseContent() { }
    public Guid Id { get; private set; }
    public Guid CaseFileId { get; private set; }
    public string ContentType { get; private set; } = "application/octet-stream";
    public string Ciphertext { get; private set; } = "";
    public string KeyVersion { get; private set; } = "";
    public string Algorithm { get; private set; } = "";
    public string AssociatedData { get; private set; } = "";
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public static ProtectedCaseContent Create(Guid caseFileId, string contentType, EncryptedEnvelope envelope, DateTimeOffset createdAtUtc)
        => new() { Id = Guid.NewGuid(), CaseFileId = caseFileId, ContentType = contentType,
            Ciphertext = envelope.Ciphertext, KeyVersion = envelope.KeyVersion, Algorithm = envelope.Algorithm,
            AssociatedData = envelope.AssociatedData, CreatedAtUtc = createdAtUtc };
    public EncryptedEnvelope Envelope => new(Ciphertext, KeyVersion, Algorithm, AssociatedData);

    public EncryptedEnvelope EnvelopeFor(Guid expectedCaseId)
    {
        if (CaseFileId != expectedCaseId || !string.Equals(AssociatedData,
            $"CaseFile:{expectedCaseId:D}", StringComparison.Ordinal))
            throw new InvalidOperationException("Protected content does not match the requested case context.");
        return Envelope;
    }
}
