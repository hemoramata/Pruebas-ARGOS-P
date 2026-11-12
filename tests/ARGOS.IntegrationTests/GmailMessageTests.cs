using ARGOS.Infrastructure.Persistence;

namespace ARGOS.IntegrationTests;

public sealed class GmailMessageTests
{
    [Fact]
    public void MessageHasOneRecipientAndNoCodeInSubject()
    {
        using var message = GmailVerificationDelivery.CreateMessage("sender@example.com", "receiver@example.com", "012345");
        Assert.Single(message.To);
        Assert.Empty(message.CC);
        Assert.Empty(message.Bcc);
        Assert.Contains("012345", message.Body);
        Assert.DoesNotContain("012345", message.Subject);
        Assert.False(message.IsBodyHtml);
    }

    [Theory]
    [InlineData("a@example.com,b@example.com", "123456")]
    [InlineData("invalid", "123456")]
    [InlineData("a@example.com", "12\r\n34")]
    public void InvalidRecipientOrCodeIsRejected(string destination, string code)
    {
        Assert.ThrowsAny<Exception>(() => GmailVerificationDelivery.CreateMessage("sender@example.com", destination, code));
    }
}
