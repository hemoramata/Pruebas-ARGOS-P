using ARGOS.Domain.Aggregates.CaseFile;
using ARGOS.Domain.Aggregates.CaseFile.Enums;
using ARGOS.Domain.Aggregates.CaseFile.Events;
using ARGOS.Domain.Aggregates.CaseFile.ValueObjects;
using ARGOS.Domain.Aggregates.Evidence.ValueObjects;

namespace ARGOS.UnitTests.Domain.Aggregates;

public sealed class CaseFileEvidenceTests
{
    [Fact]
    public void SameEvidence_CanBeUsedByDifferentCaseFilesWithoutDuplication()
    {
        var evidenceId = EvidenceId.New();
        var firstCaseFile = CreateCaseFile("TT-01-0001-2026");
        var secondCaseFile = CreateCaseFile("TT-01-0002-2026");

        var firstLink = Link(firstCaseFile, evidenceId);
        var secondLink = Link(secondCaseFile, evidenceId);

        Assert.True(firstLink.IsSuccess);
        Assert.True(secondLink.IsSuccess);
        Assert.Equal(evidenceId, firstLink.Value.EvidenceId);
        Assert.Equal(evidenceId, secondLink.Value.EvidenceId);
        Assert.NotEqual(firstLink.Value.Id, secondLink.Value.Id);
    }

    [Fact]
    public void LinkEvidence_RecordsProcedurePolicyPositionAndTraceability()
    {
        var caseFile = CreateCaseFile("TT-01-0001-2026");
        caseFile.ClearDomainEvents();
        var evidenceId = EvidenceId.New();
        var procedureVersionId = Guid.NewGuid();
        var positionId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var result = caseFile.LinkEvidence(
            evidenceId,
            " req-001 ",
            procedureVersionId,
            "POLICY-3",
            positionId,
            now.AddMinutes(-1),
            now);

        Assert.True(result.IsSuccess);
        Assert.Equal("REQ-001", result.Value.RequirementCode);
        Assert.Equal(procedureVersionId, result.Value.ProcedureVersionId);
        Assert.Equal("POLICY-3", result.Value.PolicyVersion);
        Assert.Equal(positionId, result.Value.LinkedByPositionId);
        Assert.IsType<CaseFileEvidenceLinkedDomainEvent>(
            Assert.Single(caseFile.DomainEvents));
    }

    [Fact]
    public void SameEvidenceAndRequirement_CannotBeLinkedTwice()
    {
        var caseFile = CreateCaseFile("TT-01-0001-2026");
        var evidenceId = EvidenceId.New();
        Assert.True(Link(caseFile, evidenceId).IsSuccess);

        var duplicate = Link(caseFile, evidenceId);

        Assert.True(duplicate.IsFailure);
        Assert.Equal("CaseFile.DuplicateEvidenceLink", duplicate.Error.Code);
        Assert.Single(caseFile.EvidenceLinks);
    }

    [Fact]
    public void FinalizedCaseFile_CannotReceiveEvidenceWithoutReactivation()
    {
        var caseFile = CreateCaseFile("TT-01-0001-2026");
        caseFile.StartProcessing();
        caseFile.FinalizeWithResult("APPROVED");

        var result = Link(caseFile, EvidenceId.New());

        Assert.True(result.IsFailure);
        Assert.Equal("CaseFile.EvidenceLinkNotAllowed", result.Error.Code);
        Assert.Equal(CaseFileStatus.Finalized, caseFile.Status);
    }

    [Fact]
    public void LinkEvidence_RejectsValidationAfterTheLinkDate()
    {
        var caseFile = CreateCaseFile("TT-01-0001-2026");
        var now = DateTimeOffset.UtcNow;

        var result = caseFile.LinkEvidence(
            EvidenceId.New(),
            "REQ-001",
            Guid.NewGuid(),
            "POLICY-1",
            Guid.NewGuid(),
            now.AddMinutes(1),
            now);

        Assert.True(result.IsFailure);
        Assert.Equal("CaseFile.InvalidEvidenceLinkDates", result.Error.Code);
        Assert.Empty(caseFile.EvidenceLinks);
    }

    private static ARGOS.SharedKernel.Results.Result<CaseFileEvidence> Link(
        CaseFile caseFile,
        EvidenceId evidenceId)
    {
        var now = DateTimeOffset.UtcNow;
        return caseFile.LinkEvidence(
            evidenceId,
            "REQ-001",
            Guid.NewGuid(),
            "POLICY-1",
            Guid.NewGuid(),
            now,
            now);
    }

    private static CaseFile CreateCaseFile(string number)
    {
        return new CaseFile(Guid.NewGuid(), CaseFileNumber.Create(number).Value);
    }
}
