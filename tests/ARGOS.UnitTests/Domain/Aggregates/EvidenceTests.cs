using ARGOS.Domain.Aggregates.Evidence;
using ARGOS.Domain.Aggregates.Evidence.Enums;
using ARGOS.Domain.Aggregates.Evidence.Events;

namespace ARGOS.UnitTests.Domain.Aggregates;

public sealed class EvidenceTests
{
    [Fact]
    public void Create_RequiresProvenanceAndNormalizesConfigurableCodes()
    {
        var now = DateTimeOffset.UtcNow;
        var source = CreateSource("contributor", "CIT-001", "Applicant", now);

        var result = Evidence.Create(
            Guid.NewGuid(),
            " technical-report ",
            " property ",
            "PROP-100",
            source,
            now);

        Assert.True(result.IsSuccess);
        var evidence = result.Value;
        Assert.Equal("TECHNICAL-REPORT", evidence.EvidenceTypeCode);
        Assert.Equal("PROPERTY", evidence.SubjectTypeCode);
        Assert.Equal(EvidenceStatus.Active, evidence.Status);
        Assert.Same(source, Assert.Single(evidence.Sources));
        Assert.IsType<EvidenceCreatedDomainEvent>(
            Assert.Single(evidence.DomainEvents));
    }

    [Fact]
    public void Evidence_CanPreserveMultipleIndependentSources()
    {
        var now = DateTimeOffset.UtcNow;
        var contributor = CreateSource(
            "contributor",
            "CIT-001",
            "Applicant",
            now);
        var inspector = CreateSource(
            "inspector",
            "EMP-100",
            "Institutional inspector",
            now);
        var evidence = Evidence.Create(
            Guid.NewGuid(),
            "INSPECTION-REPORT",
            "PROPERTY",
            "PROP-100",
            contributor,
            now).Value;
        evidence.ClearDomainEvents();

        var result = evidence.AddSource(inspector);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, evidence.Sources.Count);
        Assert.IsType<EvidenceSourceAddedDomainEvent>(
            Assert.Single(evidence.DomainEvents));
    }

    [Fact]
    public void Source_RejectsReceptionBeforeProduction()
    {
        var producedAt = DateTimeOffset.UtcNow;

        var result = EvidenceSource.Create(
            "EXTERNAL-INSTITUTION",
            "INST-001",
            "Issuer",
            producedAt,
            producedAt.AddMinutes(-1),
            "INTEROPERABILITY");

        Assert.True(result.IsFailure);
        Assert.Equal("Evidence.InvalidSourceDates", result.Error.Code);
    }

    [Fact]
    public void Evidence_RejectsAnEmptyCustodian()
    {
        var now = DateTimeOffset.UtcNow;
        var source = CreateSource("SYSTEM", "SYS-001", "Producer", now);

        var result = Evidence.Create(
            Guid.Empty,
            "SYSTEM-RESULT",
            "PERSON",
            "P-001",
            source,
            now);

        Assert.True(result.IsFailure);
        Assert.Equal("Evidence.InvalidData", result.Error.Code);
    }

    private static EvidenceSource CreateSource(
        string type,
        string reference,
        string role,
        DateTimeOffset now)
    {
        return EvidenceSource.Create(
            type,
            reference,
            role,
            now.AddMinutes(-5),
            now,
            "PORTAL").Value;
    }
}
