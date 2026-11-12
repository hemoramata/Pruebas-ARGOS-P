namespace ARGOS.Application.Security;

public sealed record RecoveryTarget(Guid PersonId, string Version);
public sealed record RecoveryChallenge(Guid Id, Guid Binding, DateTimeOffset ExpiresAt);
public interface IPasswordRecoveryAccounts
{
    Task<RecoveryTarget?> FindAsync(string identification, CancellationToken ct);
    Task<bool> ResetAsync(RecoveryTarget target, string password, Guid verification, CancellationToken ct);
}
public sealed class UnconfiguredRecoveryAccounts : IPasswordRecoveryAccounts
{
    public Task<RecoveryTarget?> FindAsync(string identification,CancellationToken ct) => Task.FromResult<RecoveryTarget?>(null);
    public Task<bool> ResetAsync(RecoveryTarget target,string password,Guid verification,CancellationToken ct) => Task.FromResult(false);
}

/// <summary>Single-node, expiring one-use recovery. Never stores new passwords.</summary>
public sealed class PasswordRecovery(IPasswordRecoveryAccounts accounts, IVerificationDelivery delivery, TimeProvider clock, ActiveSessions sessions) : IDisposable
{
    private sealed record Pending(RecoveryTarget Target, Guid Binding, SecondFactorChallenge Challenge);
    private readonly Dictionary<Guid,Pending> pending = [];
    private readonly Dictionary<Guid,DateTimeOffset> cooldown = [];
    private readonly SemaphoreSlim gate = new(1,1);
    public async Task<RecoveryChallenge> BeginAsync(string identification,CancellationToken ct)
    {
        var hidden = new RecoveryChallenge(Guid.NewGuid(),Guid.NewGuid(),clock.GetUtcNow().AddMinutes(3));
        if(string.IsNullOrWhiteSpace(identification) || identification.Length>100) return hidden;
        var target=await accounts.FindAsync(identification.Trim(),ct);
        if(target is null) return hidden;
        await gate.WaitAsync(ct);
        try
        {
            var now=clock.GetUtcNow();
            foreach(var id in pending.Where(x=>x.Value.Challenge.ExpiresAt<=now).Select(x=>x.Key).ToArray()) { pending[id].Challenge.Dispose(); pending.Remove(id); }
            foreach(var id in cooldown.Where(x=>x.Value<=now).Select(x=>x.Key).ToArray()) cooldown.Remove(id);
            if(pending.Count>=1000 || cooldown.ContainsKey(target.PersonId)) return hidden;
            cooldown[target.PersonId]=now.AddMinutes(1);
            foreach(var id in pending.Where(x=>x.Value.Target.PersonId==target.PersonId).Select(x=>x.Key).ToArray()) { pending[id].Challenge.Dispose(); pending.Remove(id); }
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(15));
            try
            {
                var challenge=await SecondFactorChallenge.IssueAsync(target.PersonId,hidden.Binding,"password-recovery",VerificationChannel.InstitutionalEmail,TimeSpan.FromMinutes(3),5,
                    (code,token)=>delivery.SendAsync(target.PersonId,VerificationChannel.InstitutionalEmail,code,token),clock,timeout.Token);
                pending.Add(challenge.Id,new(target,hidden.Binding,challenge));
                return new(challenge.Id,hidden.Binding,challenge.ExpiresAt);
            }
            catch(Exception e) when(e is InvalidOperationException or OperationCanceledException or TimeoutException) { return hidden; }
        }
        finally { gate.Release(); }
    }
    public async Task<bool> CompleteAsync(Guid id,Guid binding,string code,string password,CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            if(!pending.TryGetValue(id,out var item)) return false;
            var result=item.Challenge.Verify(item.Target.PersonId,binding,"password-recovery",code);
            if(result==VerificationResult.Rejected) return false;
            pending.Remove(id); item.Challenge.Dispose();
            if(result!=VerificationResult.Accepted) return false;
            if(!await accounts.ResetAsync(item.Target,password,id,ct)) return false;
            sessions.RevokeForPerson(item.Target.PersonId);
            return true;
        }
        finally { gate.Release(); }
    }
    public async Task CancelAsync(Guid id,Guid binding,CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try { if(pending.TryGetValue(id,out var item)&&item.Binding==binding) { pending.Remove(id); item.Challenge.Dispose(); } }
        finally { gate.Release(); }
    }
    public void Dispose() { foreach(var item in pending.Values) item.Challenge.Dispose(); gate.Dispose(); }
}
