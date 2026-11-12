using System.Text.Json;

namespace ARGOS.Infrastructure.Persistence;

public sealed record ActionAuthority(string Action, Guid PositionId, string CompetenceCode);
public sealed record EvidenceRequirementRule(string RequirementCode, string EvidenceTypeCode, string SubjectTypeCode,
    int MaximumAgeDays, bool ReuseAllowed);
public sealed record InstitutionalPolicyRules(string ProcedureCode, ActionAuthority[] Authorities,
    int RequiredApprovalCount, bool SeparationOfDutiesRequired, bool EmergencyPublicationAllowed,
    EvidenceRequirementRule[] EvidenceRequirements);

/// <summary>Immutable institutional policy version backed by retained documentary content.</summary>
public sealed class InstitutionalOperationPolicy
{
    private InstitutionalOperationPolicy() { }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string ProcedureCode { get; private set; } = "";
    public int Version { get; private set; }
    public Guid ProofContentId { get; private set; }
    public Guid ApprovedBy { get; private set; }
    public Guid ApprovalAssignmentId { get; private set; }
    public Guid VerificationId { get; private set; }
    public DateTimeOffset PublishedAt { get; private set; }
    public string RulesJson { get; private set; } = "";
    public InstitutionalPolicyRules Rules => JsonSerializer.Deserialize<InstitutionalPolicyRules>(RulesJson)!;
    public static readonly string[] Actions = ["submit-review", "approve", "reject", "publish", "publish-emergency",
        "ratify-emergency", "retire", "archive", "evidence-link"];

    public static bool Valid(InstitutionalPolicyRules? rules) => rules is not null
        && !string.IsNullOrWhiteSpace(rules.ProcedureCode) && rules.ProcedureCode.Length <= 50
        && rules.ProcedureCode == rules.ProcedureCode.Trim().ToUpperInvariant()
        && rules.RequiredApprovalCount is >= 1 and <= 10
        && rules.Authorities is { Length: > 0 and <= 100 }
        && rules.Authorities.All(x => x is not null && Actions.Contains(x.Action) && x.PositionId != Guid.Empty
            && !string.IsNullOrWhiteSpace(x.CompetenceCode) && x.CompetenceCode.Length <= 50
            && x.CompetenceCode == x.CompetenceCode.Trim().ToUpperInvariant())
        && rules.EvidenceRequirements is { Length: <= 100 }
        && rules.EvidenceRequirements.All(x => x is not null && !string.IsNullOrWhiteSpace(x.RequirementCode)
            && x.RequirementCode.Length <= 100 && x.RequirementCode == x.RequirementCode.Trim().ToUpperInvariant()
            && !string.IsNullOrWhiteSpace(x.EvidenceTypeCode) && x.EvidenceTypeCode.Length <= 100
            && x.EvidenceTypeCode == x.EvidenceTypeCode.Trim().ToUpperInvariant()
            && !string.IsNullOrWhiteSpace(x.SubjectTypeCode) && x.SubjectTypeCode.Length <= 100
            && x.SubjectTypeCode == x.SubjectTypeCode.Trim().ToUpperInvariant()
            && x.MaximumAgeDays is > 0 and <= 36500)
        && rules.EvidenceRequirements.Select(x => x.RequirementCode).Distinct().Count() == rules.EvidenceRequirements.Length;

    public static InstitutionalOperationPolicy Create(Guid organization, int version, Guid proof, Guid person,
        Guid assignment, Guid verification, InstitutionalPolicyRules rules, DateTimeOffset now)
    {
        if (!Valid(rules) || organization == Guid.Empty || proof == Guid.Empty || person == Guid.Empty
            || assignment == Guid.Empty || verification == Guid.Empty || version < 1) throw new ArgumentException("Invalid policy.");
        return new() { Id = Guid.NewGuid(), OrganizationId = organization, Version = version, ProcedureCode = rules.ProcedureCode,
            ProofContentId = proof, ApprovedBy = person, ApprovalAssignmentId = assignment, VerificationId = verification,
            PublishedAt = now, RulesJson = JsonSerializer.Serialize(rules) };
    }
}
