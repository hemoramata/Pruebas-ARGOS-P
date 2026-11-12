using ARGOS.Domain.Aggregates.CaseFile;
using ARGOS.Domain.Aggregates.CaseFile.Enums;
using ARGOS.Domain.Aggregates.CaseFile.ValueObjects;
using ARGOS.Domain.Aggregates.Evidence.ValueObjects;

namespace ARGOS.UnitTests.Domain.Aggregates;

public sealed class CaseFileInputLimitsTests
{
    [Theory]
    [InlineData(2000, true)]
    [InlineData(2001, false)]
    public void HoldRespectsStorageLimitWithoutPartialChanges(int length, bool accepted)
    {
        var item = Create();
        item.ClearDomainEvents();
        Assert.Equal(accepted, item.PutOnHold(" " + new string('x', length) + " ").IsSuccess);
        Assert.Equal(accepted ? CaseFileStatus.Waiting : CaseFileStatus.InProgress, item.Status);
        if (!accepted) { Assert.Null(item.WaitingReason); Assert.Empty(item.DomainEvents); }
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void ResultRespectsStorageLimitWithoutPartialChanges(int length, bool accepted)
    {
        var item = Create();
        item.ClearDomainEvents();
        Assert.Equal(accepted, item.FinalizeWithResult(new string('X', length)).IsSuccess);
        Assert.Equal(accepted ? CaseFileStatus.Finalized : CaseFileStatus.InProgress, item.Status);
        if (!accepted) { Assert.Null(item.FinalResultCode); Assert.Empty(item.DomainEvents); }
    }

    [Theory]
    [InlineData(100, 100, true)]
    [InlineData(101, 100, false)]
    [InlineData(100, 101, false)]
    public void EvidenceLinkRespectsStorageLimits(int requirementLength, int policyLength, bool accepted)
    {
        var item = Create();
        item.ClearDomainEvents();
        var now = DateTimeOffset.UtcNow;
        var result = item.LinkEvidence(EvidenceId.New(), new string('R', requirementLength), Guid.NewGuid(),
            new string('P', policyLength), Guid.NewGuid(), now, now);
        Assert.Equal(accepted, result.IsSuccess);
        if (!accepted) { Assert.Empty(item.EvidenceLinks); Assert.Empty(item.DomainEvents); }
    }

    private static CaseFile Create()
    {
        var item = new CaseFile(Guid.NewGuid(), CaseFileNumber.Create("TT-01-0001-2026").Value);
        item.StartProcessing();
        return item;
    }
}
