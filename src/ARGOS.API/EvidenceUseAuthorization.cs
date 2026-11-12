using System.Security.Claims;
using ARGOS.Domain.Aggregates.CaseFile;
using ARGOS.Domain.Aggregates.Evidence;
using ARGOS.Domain.Aggregates.Evidence.Enums;
using ARGOS.Domain.Aggregates.Procedure.ValueObjects;
using ARGOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ARGOS.API;

public sealed record AuthorizedEvidenceUse(ResolvedAuthority Authority, Guid PolicyId, DateTimeOffset Now);
public sealed class EvidenceUseAuthorization(ArgosDbContext db, InstitutionalAuthorityResolver resolver, TimeProvider clock,
    ARGOS.Application.Security.IEnvelopeEncryptionProvider encryption)
{
    public async Task<AuthorizedEvidenceUse?> Resolve(HttpContext http, CaseFile item, Evidence evidence,
        Guid assignmentId, string competenceCode, string requirementCode, CancellationToken ct)
    {
        if (!Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var person)
            || !Guid.TryParse(http.User.FindFirstValue("organization_id"), out var org)
            || item.ProcedureVersionId is null || item.SubjectTypeCode is null || item.SubjectReference is null
            || evidence.CustodianOrganizationId != org || evidence.Status != EvidenceStatus.Active
            || evidence.SubjectTypeCode != item.SubjectTypeCode || evidence.SubjectReference != item.SubjectReference
            || evidence.ProtectedContentId is null) return null;
        var authority = await resolver.ResolveAsync(person, org, assignmentId, competenceCode, "case-files.update", ct);
        if (authority is null) return null;
        var procedure = await db.Procedures.AsNoTracking().SingleOrDefaultAsync(x => x.Id == ProcedureId.From(item.ProcedureVersionId.Value), ct);
        if (procedure is null || !Guid.TryParse(procedure.ApprovalPolicyVersion, out var policyId)) return null;
        var policy = await db.OperationPolicies.AsNoTracking().SingleOrDefaultAsync(x => x.Id == policyId && x.OrganizationId == org, ct);
        if (policy is null || !policy.Rules.Authorities.Any(x => x.Action == "evidence-link" && x.PositionId == authority.Position
            && x.CompetenceCode == authority.Competence)) return null;
        var rule = policy.Rules.EvidenceRequirements.SingleOrDefault(x => x.RequirementCode == requirementCode.Trim().ToUpperInvariant());
        if (rule is null || rule.EvidenceTypeCode != evidence.EvidenceTypeCode || rule.SubjectTypeCode != evidence.SubjectTypeCode) return null;
        var now = clock.GetUtcNow();
        if (evidence.Sources.Count == 0 || evidence.CreatedAtUtc > now || evidence.Sources.Any(x => x.ProducedAtUtc > now
            || x.ReceivedAtUtc > now || x.ProducedAtUtc < now.AddDays(-rule.MaximumAgeDays))) return null;
        var content = await db.ProtectedCaseContents.AsNoTracking().SingleOrDefaultAsync(x => x.Id == evidence.ProtectedContentId, ct);
        if (content is null || string.IsNullOrWhiteSpace(content.Ciphertext)
            || !await db.AuditEntries.AnyAsync(x => x.OrganizationId == org && x.ActionCode == "CaseFile.Created"
                && x.SubjectType == "CaseFile" && x.SubjectReference == content.CaseFileId.ToString() && x.Outcome == "Succeeded", ct)) return null;
        if (!rule.ReuseAllowed && await db.Set<CaseFileEvidence>().AnyAsync(x => x.EvidenceId == evidence.Id, ct)) return null;
        try {
            var verified = await encryption.DecryptAsync(content.EnvelopeFor(content.CaseFileId), ct);
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(verified);
        }
        catch (InvalidOperationException) { return null; }
        return new(authority, policyId, now);
    }
}
