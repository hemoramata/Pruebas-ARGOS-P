using ARGOS.Domain.Aggregates.CaseFile;
using ARGOS.Domain.Aggregates.CaseFile.Enums;
using ARGOS.Domain.Aggregates.CaseFile.Events;
using ARGOS.Domain.Aggregates.CaseFile.ValueObjects;

namespace ARGOS.UnitTests.Domain.Aggregates;

public sealed class CaseFileTests
{
    [Fact]
    public void Create_UsesTheOfficialCreatedStatusAndRaisesAnEvent()
    {
        var caseFile = CreateCaseFile();

        Assert.Equal(CaseFileStatus.Created, caseFile.Status);
        Assert.Null(caseFile.WaitingReason);
        Assert.Null(caseFile.FinalResultCode);
        Assert.IsType<CaseFileCreatedDomainEvent>(Assert.Single(caseFile.DomainEvents));
    }

    [Fact]
    public void Lifecycle_CanFollowTheOrdinaryInstitutionalRoute()
    {
        var caseFile = CreateCaseFile();

        Assert.True(caseFile.StartProcessing().IsSuccess);
        Assert.Equal(CaseFileStatus.InProgress, caseFile.Status);

        Assert.True(caseFile.PutOnHold("Waiting for an inspection").IsSuccess);
        Assert.Equal(CaseFileStatus.Waiting, caseFile.Status);
        Assert.Equal("Waiting for an inspection", caseFile.WaitingReason);

        Assert.True(caseFile.ResumeProcessing().IsSuccess);
        Assert.Equal(CaseFileStatus.InProgress, caseFile.Status);
        Assert.Null(caseFile.WaitingReason);

        Assert.True(caseFile.FinalizeWithResult(" approved ").IsSuccess);
        Assert.Equal(CaseFileStatus.Finalized, caseFile.Status);
        Assert.Equal("APPROVED", caseFile.FinalResultCode);

        Assert.True(caseFile.Archive().IsSuccess);
        Assert.Equal(CaseFileStatus.Archived, caseFile.Status);
    }

    [Fact]
    public void PutOnHold_RequiresAReasonAndLeavesTheStateUnchanged()
    {
        var caseFile = CreateCaseFile();
        caseFile.StartProcessing();

        var result = caseFile.PutOnHold(" ");

        Assert.True(result.IsFailure);
        Assert.Equal("CaseFile.WaitingReasonRequired", result.Error.Code);
        Assert.Equal(CaseFileStatus.InProgress, caseFile.Status);
    }

    [Fact]
    public void Finalize_RequiresADefinitiveResultAndLeavesTheStateUnchanged()
    {
        var caseFile = CreateCaseFile();
        caseFile.StartProcessing();

        var result = caseFile.FinalizeWithResult(string.Empty);

        Assert.True(result.IsFailure);
        Assert.Equal("CaseFile.FinalResultRequired", result.Error.Code);
        Assert.Equal(CaseFileStatus.InProgress, caseFile.Status);
    }

    [Fact]
    public void InvalidTransition_IsRejectedWithoutChangingTheCaseFile()
    {
        var caseFile = CreateCaseFile();

        var result = caseFile.Archive();

        Assert.True(result.IsFailure);
        Assert.Equal("CaseFile.InvalidStatusTransition", result.Error.Code);
        Assert.Equal(CaseFileStatus.Created, caseFile.Status);
    }

    [Fact]
    public void EverySuccessfulTransition_RaisesATraceableDomainEvent()
    {
        var caseFile = CreateCaseFile();
        caseFile.ClearDomainEvents();

        caseFile.StartProcessing();

        var domainEvent = Assert.IsType<CaseFileStatusChangedDomainEvent>(
            Assert.Single(caseFile.DomainEvents));

        Assert.Equal(CaseFileStatus.Created, domainEvent.PreviousStatus);
        Assert.Equal(CaseFileStatus.InProgress, domainEvent.NewStatus);
        Assert.False(string.IsNullOrWhiteSpace(domainEvent.Reason));
    }

    private static CaseFile CreateCaseFile()
    {
        return new CaseFile(
            Guid.NewGuid(),
            CaseFileNumber.Create("TT-01-0001-2026").Value);
    }
}
