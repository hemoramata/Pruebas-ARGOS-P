namespace ARGOS.Application.Security;

public enum SensitiveDecisionStatus { Pending, Authorized, Executed, Rejected, Expired }

public sealed class SensitiveDecision
{
    private SensitiveDecision() { }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid OrganizationId { get; private set; }
    public string OperationCode { get; private set; } = "";
    public string SubjectType { get; private set; } = "";
    public string SubjectReference { get; private set; } = "";
    public Guid InitiatedBy { get; private set; }
    public DateTimeOffset InitiatedAt { get; private set; }
    public SensitiveDecisionStatus Status { get; private set; }
    public Guid? AuthorizedBy { get; private set; }
    public DateTimeOffset? AuthorizedAt { get; private set; }
    public string? VerificationReference { get; private set; }
    public string? AuthorizationReference { get; private set; }
    public DateTimeOffset? ExecutedAt { get; private set; }
    public string? ExecutionReference { get; private set; }
    public string? RejectionReference { get; private set; }
    public static SensitiveDecision Begin(Guid organizationId,string operationCode,string subjectType,string subjectReference,Guid initiatedBy,DateTimeOffset initiatedAt) {
        if(organizationId==Guid.Empty||initiatedBy==Guid.Empty) throw new ArgumentException("Organization and initiator are required.");
        return new SensitiveDecision { OrganizationId=organizationId, OperationCode=Required(operationCode,100), SubjectType=Required(subjectType,100), SubjectReference=Required(subjectReference,200), InitiatedBy=initiatedBy, InitiatedAt=initiatedAt.ToUniversalTime(), Status=SensitiveDecisionStatus.Pending };
    }
    public void Authorize(Guid authorizedBy,string verificationReference,string authorizationReference,DateTimeOffset authorizedAt) {
        if(Status!=SensitiveDecisionStatus.Pending) throw new InvalidOperationException("Only pending decisions can be authorized.");
        if(authorizedBy==Guid.Empty) throw new ArgumentException("Authorizer is required.");
        AuthorizedBy=authorizedBy; AuthorizedAt=authorizedAt.ToUniversalTime(); VerificationReference=Required(verificationReference,200); AuthorizationReference=Required(authorizationReference,200); Status=SensitiveDecisionStatus.Authorized;
    }
    public void MarkExecuted(string executionReference,DateTimeOffset executedAt) { if(Status!=SensitiveDecisionStatus.Authorized) throw new InvalidOperationException("A decision must be authorized before execution."); ExecutedAt=executedAt.ToUniversalTime(); ExecutionReference=Required(executionReference,200); Status=SensitiveDecisionStatus.Executed; }
    public void Reject(string rejectionReference) { if(Status is SensitiveDecisionStatus.Executed or SensitiveDecisionStatus.Rejected) throw new InvalidOperationException("The decision is already closed."); RejectionReference=Required(rejectionReference,200); Status=SensitiveDecisionStatus.Rejected; }
    private static string Required(string? value,int max) { if(string.IsNullOrWhiteSpace(value)||value.Trim().Length>max) throw new ArgumentException("Required decision data is invalid."); return value.Trim(); }
}