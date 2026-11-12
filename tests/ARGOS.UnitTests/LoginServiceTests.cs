using ARGOS.Application.Security;

namespace ARGOS.UnitTests;

public sealed class LoginServiceTests
{
    [Fact]
    public async Task CancellationInvalidatesCodeOnlyWithCorrectBinding()
    {
        var adapter = new Adapter();
        using var service = new LoginService(adapter, adapter, TimeProvider.System);
        var challenge = (await service.BeginAsync("test", "valid", default))!;
        await service.CancelAsync(challenge.Id, Guid.NewGuid(), default);
        await service.CancelAsync(challenge.Id, challenge.Binding, default);
        Assert.Null(await service.CompleteAsync(challenge.Id, challenge.Binding, adapter.Code, default));
    }

    [Fact]
    public async Task SuccessfulFlowDoesNotAllowReplay()
    {
        var adapter = new Adapter();
        using var service = new LoginService(adapter, adapter, TimeProvider.System);
        var challenge = await service.BeginAsync("test", "valid", default);
        Assert.NotNull(challenge);
        Assert.NotNull(await service.CompleteAsync(challenge.Id, challenge.Binding, adapter.Code, default));
        Assert.Null(await service.CompleteAsync(challenge.Id, challenge.Binding, adapter.Code, default));
    }

    [Fact]
    public async Task RevocationBetweenPasswordAndCodePreventsSession()
    {
        var adapter = new Adapter();
        using var service = new LoginService(adapter, adapter, TimeProvider.System);
        var challenge = (await service.BeginAsync("test", "valid", default))!;
        adapter.Active = false;
        Assert.Null(await service.CompleteAsync(challenge.Id, challenge.Binding, adapter.Code, default));
    }

    [Fact]
    public async Task WrongPasswordDoesNotSendAndResendsAreLimited()
    {
        var adapter = new Adapter();
        using var service = new LoginService(adapter, adapter, TimeProvider.System);
        Assert.Null(await service.BeginAsync("test", "wrong", default));
        Assert.Equal(0, adapter.Sends);
        Assert.NotNull(await service.BeginAsync("test", "valid", default));
        Assert.Null(await service.BeginAsync("test", "valid", default));
        Assert.Equal(1, adapter.Sends);
    }

    [Fact]
    public async Task WrongBindingAndWrongCodeCannotAuthenticate()
    {
        var adapter = new Adapter();
        using var service = new LoginService(adapter, adapter, TimeProvider.System);
        var challenge = (await service.BeginAsync("test", "valid", default))!;
        Assert.Null(await service.CompleteAsync(challenge.Id, Guid.NewGuid(), adapter.Code, default));
        Assert.Null(await service.CompleteAsync(challenge.Id, challenge.Binding, "bad", default));
        Assert.NotNull(await service.CompleteAsync(challenge.Id, challenge.Binding, adapter.Code, default));
    }

    [Fact]
    public async Task PasswordRotationInvalidatesAlreadyIssuedLoginCode()
    {
        var adapter=new Adapter(); using var service=new LoginService(adapter,adapter,TimeProvider.System);
        var challenge=(await service.BeginAsync("test","valid",default))!;
        adapter.RotateCredential();
        Assert.Null(await service.CompleteAsync(challenge.Id,challenge.Binding,adapter.Code,default));
    }
    private sealed class Adapter : ILoginAccounts, IVerificationDelivery
    {
        public string Code { get; private set; } = "";
        public int Sends { get; private set; }
        public bool Active { get; set; } = true;
        public void RotateCredential() => account = account with { CredentialVersion="rotated" };
        private LoginAccount account = new(Guid.NewGuid(), Guid.NewGuid(), "Persona de prueba",
            "***1234", VerificationChannel.Sms, ["personnel.manage"]);
        public Task<LoginAccount?> ValidatePasswordAsync(string identification, string password, CancellationToken ct)
            => Task.FromResult<LoginAccount?>(Active && identification == "test" && password == "valid" ? account : null);
        public Task<LoginAccount?> GetActiveAsync(Guid personId, CancellationToken ct)
            => Task.FromResult<LoginAccount?>(Active && personId == account.PersonId ? account : null);
        public Task SendAsync(Guid personId, VerificationChannel channel, string code, CancellationToken ct)
        { Code = code; Sends++; return Task.CompletedTask; }
    }
}
