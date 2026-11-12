using ARGOS.Application.Security;
using ARGOS.Infrastructure.Persistence;

namespace ARGOS.UnitTests;

public sealed class ProtectedCaseContentTests
{
    [Fact]
    public void StoredContextMustMatchTrustedRequestedCase()
    {
        var id = Guid.NewGuid();
        var envelope = new EncryptedEnvelope("vault:v1:encoded", "v1", "openbao-transit", $"CaseFile:{id:D}");
        var content = ProtectedCaseContent.Create(id, "application/pdf", envelope, DateTimeOffset.UtcNow);
        Assert.Equal(envelope, content.EnvelopeFor(id));
        Assert.Throws<InvalidOperationException>(() => content.EnvelopeFor(Guid.NewGuid()));
    }

    [Fact]
    public void MovingEnvelopeToAnotherCaseDoesNotAuthorizeDecryption()
    {
        var source = Guid.NewGuid(); var target = Guid.NewGuid();
        var envelope = new EncryptedEnvelope("vault:v1:encoded", "v1", "openbao-transit", $"CaseFile:{source:D}");
        var moved = ProtectedCaseContent.Create(target, "application/pdf", envelope, DateTimeOffset.UtcNow);
        Assert.Throws<InvalidOperationException>(() => moved.EnvelopeFor(target));
    }
}
