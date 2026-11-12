using ARGOS.Application.Security;

namespace ARGOS.UnitTests;

public sealed class PasswordRecoveryTests
{
    private sealed class Clock : TimeProvider
    { public DateTimeOffset Now=DateTimeOffset.UtcNow; public override DateTimeOffset GetUtcNow()=>Now; }
    private sealed class Adapter : IPasswordRecoveryAccounts, IVerificationDelivery
    {
        public RecoveryTarget Target=new(Guid.NewGuid(),"v1");
        public string Code=""; public int Sends,Resets; public bool Found=true,Accept=true,FailSend;
        public Task<RecoveryTarget?> FindAsync(string identification,CancellationToken ct)=>Task.FromResult<RecoveryTarget?>(Found?Target:null);
        public Task<bool> ResetAsync(RecoveryTarget target,string password,Guid verification,CancellationToken ct) { if(Accept) Resets++; return Task.FromResult(Accept); }
        public Task SendAsync(Guid personId,VerificationChannel channel,string code,CancellationToken ct)
        { if(FailSend) throw new InvalidOperationException("provider details must not escape"); Code=code; Sends++; return Task.CompletedTask; }
    }
    [Fact]
    public async Task SuccessIsOneUseAndRevokesSessions()
    {
        var a=new Adapter(); var c=new Clock(); var sessions=new ActiveSessions(c); var session=Guid.NewGuid(); sessions.Add(session,a.Target.PersonId);
        using var service=new PasswordRecovery(a,a,c,sessions);
        var challenge=await service.BeginAsync("001",default);
        Assert.True(await service.CompleteAsync(challenge.Id,challenge.Binding,a.Code,"New-password-1",default));
        Assert.False(sessions.Contains(session,a.Target.PersonId));
        Assert.False(await service.CompleteAsync(challenge.Id,challenge.Binding,a.Code,"New-password-1",default)); Assert.Equal(1,a.Resets);
    }
    [Theory]
    [InlineData(true,false)] [InlineData(false,true)]
    public async Task UnknownAndUndeliverableHaveOpaqueChallenge(bool missing,bool failed)
    {
        var a=new Adapter {Found=!missing,FailSend=failed}; var c=new Clock(); using var service=new PasswordRecovery(a,a,c,new ActiveSessions(c));
        var challenge=await service.BeginAsync("001",default);
        Assert.NotEqual(Guid.Empty,challenge.Id);
        Assert.False(await service.CompleteAsync(challenge.Id,challenge.Binding,"123456","New-password-1",default)); Assert.Equal(0,a.Resets);
    }
    [Fact]
    public async Task FiveFailuresLockTheChallenge()
    {
        var a=new Adapter(); var c=new Clock(); using var service=new PasswordRecovery(a,a,c,new ActiveSessions(c));
        var challenge=await service.BeginAsync("001",default);
        for(var i=0;i<5;i++) Assert.False(await service.CompleteAsync(challenge.Id,challenge.Binding,"wrong","New-password-1",default));
        Assert.False(await service.CompleteAsync(challenge.Id,challenge.Binding,a.Code,"New-password-1",default));
    }
    [Fact]
    public async Task ExpiryCancellationAndWrongBindingCannotReset()
    {
        var a=new Adapter(); var c=new Clock(); using var service=new PasswordRecovery(a,a,c,new ActiveSessions(c));
        var first=await service.BeginAsync("001",default);
        Assert.False(await service.CompleteAsync(first.Id,Guid.NewGuid(),a.Code,"New-password-1",default));
        await service.CancelAsync(first.Id,first.Binding,default);
        Assert.False(await service.CompleteAsync(first.Id,first.Binding,a.Code,"New-password-1",default));
        c.Now=c.Now.AddMinutes(1); var second=await service.BeginAsync("001",default); c.Now=c.Now.AddMinutes(3);
        Assert.False(await service.CompleteAsync(second.Id,second.Binding,a.Code,"New-password-1",default)); Assert.Equal(0,a.Resets);
    }
    [Fact]
    public async Task CooldownLimitsMailAndChangedAccountCannotReset()
    {
        var a=new Adapter {Accept=false}; var c=new Clock(); using var service=new PasswordRecovery(a,a,c,new ActiveSessions(c));
        var first=await service.BeginAsync("001",default); await service.BeginAsync("001",default); Assert.Equal(1,a.Sends);
        Assert.False(await service.CompleteAsync(first.Id,first.Binding,a.Code,"New-password-1",default)); Assert.Equal(0,a.Resets);
    }
}
