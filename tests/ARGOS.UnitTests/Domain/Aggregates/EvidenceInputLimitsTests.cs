using ARGOS.Domain.Aggregates.Evidence;

namespace ARGOS.UnitTests.Domain.Aggregates;

public sealed class EvidenceInputLimitsTests
{
    [Theory]
    [InlineData(100, 200, 200, 100, true)]
    [InlineData(101, 200, 200, 100, false)]
    [InlineData(100, 201, 200, 100, false)]
    [InlineData(100, 200, 201, 100, false)]
    [InlineData(100, 200, 200, 101, false)]
    public void SourceRejectsValuesBeyondStorageLimits(int type, int reference, int role, int channel, bool accepted)
    {
        var now = DateTimeOffset.UtcNow;
        var result = EvidenceSource.Create(new string('T', type), new string('R', reference),
            new string('R', role), now, now, new string('C', channel));
        Assert.Equal(accepted, result.IsSuccess);
    }

    [Theory]
    [InlineData(100, 100, 200, true)]
    [InlineData(101, 100, 200, false)]
    [InlineData(100, 101, 200, false)]
    [InlineData(100, 100, 201, false)]
    public void EvidenceRejectsValuesBeyondStorageLimits(int type, int subject, int reference, bool accepted)
    {
        var now = DateTimeOffset.UtcNow;
        var source = EvidenceSource.Create("PERSON", "TEST", "Author", now, now, "WEB").Value;
        var result = Evidence.Create(Guid.NewGuid(), new string('T', type), new string('S', subject),
            new string('R', reference), source, now);
        Assert.Equal(accepted, result.IsSuccess);
    }
}
