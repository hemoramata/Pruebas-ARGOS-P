using ARGOS.Application.Security;
namespace ARGOS.UnitTests;
public sealed class SensitiveDecisionTests
{
 [Fact] public void CannotExecuteBeforeAdditionalVerification() { var d=SensitiveDecision.Begin(Guid.NewGuid(),"REMOVE_CASE","CaseFile","case-1",Guid.NewGuid(),DateTimeOffset.UtcNow); Assert.Throws<InvalidOperationException>(()=>d.MarkExecuted("execution",DateTimeOffset.UtcNow)); }
 [Fact] public void AdditionalVerificationAndExecutionAreRecorded() { var org=Guid.NewGuid();var initiator=Guid.NewGuid();var authorizer=Guid.NewGuid();var d=SensitiveDecision.Begin(org,"PUBLISH","OrganizationSetup","setup-1",initiator,DateTimeOffset.UtcNow);d.Authorize(authorizer,"challenge-123","authorization-456",DateTimeOffset.UtcNow);d.MarkExecuted("execution-789",DateTimeOffset.UtcNow);Assert.Equal(SensitiveDecisionStatus.Executed,d.Status);Assert.Equal(authorizer,d.AuthorizedBy);Assert.Equal("challenge-123",d.VerificationReference); }
 [Fact] public void RejectionClosesDecision() { var d=SensitiveDecision.Begin(Guid.NewGuid(),"CHANGE_PERMISSION","Position","position-1",Guid.NewGuid(),DateTimeOffset.UtcNow);d.Reject("reason-1");Assert.Equal(SensitiveDecisionStatus.Rejected,d.Status);Assert.Throws<InvalidOperationException>(()=>d.Authorize(Guid.NewGuid(),"c","a",DateTimeOffset.UtcNow)); }
}