using ARGOS.Infrastructure.Persistence;

namespace ARGOS.UnitTests;

public sealed class InstitutionalPolicyRulesTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(10, true)]
    [InlineData(11, false)]
    public void ApprovalCountMatchesDomainLimits(int count, bool valid)
        => Assert.Equal(valid, InstitutionalOperationPolicy.Valid(Rules() with { RequiredApprovalCount = count }));

    [Fact]
    public void UnknownActionsAndDuplicateRequirementsAreRejected()
    {
        Assert.False(InstitutionalOperationPolicy.Valid(Rules() with { Authorities = [new("delete-everything", Guid.NewGuid(), "APPROVAL")] }));
        var rule = new EvidenceRequirementRule("REQ", "DOC", "PERSON", 30, true);
        Assert.False(InstitutionalOperationPolicy.Valid(Rules() with { EvidenceRequirements = [rule, rule] }));
        Assert.False(InstitutionalOperationPolicy.Valid(Rules() with { EvidenceRequirements = [rule with { MaximumAgeDays = 0 }] }));
    }

    [Theory]
    [InlineData("document", "PERSON")]
    [InlineData("DOCUMENT ", "PERSON")]
    [InlineData("DOCUMENT", "person")]
    [InlineData("DOCUMENT", " PERSON")]
    public void NonCanonicalEvidenceCodesCannotPublishAnUnusableRule(string type, string subject)
        => Assert.False(InstitutionalOperationPolicy.Valid(Rules() with {
            EvidenceRequirements = [new("REQ", type, subject, 30, true)] }));

    [Fact]
    public void MalformedCollectionsAreRejectedWithoutThrowing()
    {
        Assert.False(InstitutionalOperationPolicy.Valid(null));
        Assert.False(InstitutionalOperationPolicy.Valid(Rules() with { Authorities = null! }));
        Assert.False(InstitutionalOperationPolicy.Valid(Rules() with { Authorities = [null!] }));
        Assert.False(InstitutionalOperationPolicy.Valid(Rules() with { EvidenceRequirements = null! }));
        Assert.False(InstitutionalOperationPolicy.Valid(Rules() with { EvidenceRequirements = [null!] }));
        Assert.False(InstitutionalOperationPolicy.Valid(Rules() with { Authorities = [new("approve", Guid.NewGuid(), "approval")] }));
    }

    private static InstitutionalPolicyRules Rules() => new("P-TEST", [new("approve", Guid.NewGuid(), "APPROVAL")], 1, true, false, []);
}
