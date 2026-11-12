using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ARGOS.Application.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ARGOS.Infrastructure.Persistence;

public sealed class PostgresPasswordRecovery(IServiceScopeFactory scopes,TimeProvider clock) : IPasswordRecoveryAccounts
{
    private static string Version(AccessAccount a) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        JsonSerializer.Serialize(new { Salt=Convert.ToBase64String(a.Salt), a.Destination, a.VerificationReference, a.Channel }))));
    private static bool Eligible(AccessAccount a) => a.Enabled && a.Channel==VerificationChannel.InstitutionalEmail && a.VerificationReference.StartsWith("email-challenge:");
    public async Task<RecoveryTarget?> FindAsync(string identification,CancellationToken ct)
    {
        await using var scope=scopes.CreateAsyncScope();
        var db=scope.ServiceProvider.GetRequiredService<AccessDbContext>();
        var ids=await db.Database.SqlQuery<Guid>($"""
            SELECT a."PersonId" AS "Value" FROM "AccessAccounts" a
            JOIN "PersonnelPeople" p ON p."Id"=a."PersonId"
            WHERE p."Identity"::jsonb ->> 'Number' = {identification}
            """).Take(2).ToListAsync(ct);
        if(ids.Count!=1) return null;
        var account=await db.Accounts.AsNoTracking().SingleAsync(a=>a.PersonId==ids[0],ct);
        return Eligible(account) ? new(account.PersonId,Version(account)) : null;
    }
    public async Task<bool> ResetAsync(RecoveryTarget target,string password,Guid verification,CancellationToken ct)
    {
        if(verification==Guid.Empty) return false;
        AccessAccount.ValidatePasswordPolicy(password);
        await using var scope=scopes.CreateAsyncScope();
        var db=scope.ServiceProvider.GetRequiredService<AccessDbContext>();
        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        var account=await db.Accounts.FromSqlInterpolated($"SELECT * FROM \"AccessAccounts\" WHERE \"PersonId\"={target.PersonId} FOR UPDATE").SingleOrDefaultAsync(ct);
        if(account is null || !Eligible(account) || Version(account)!=target.Version) return false;
        var person=await scope.ServiceProvider.GetRequiredService<PersonnelDbContext>().People.AsNoTracking().SingleOrDefaultAsync(p=>p.Id==target.PersonId,ct);
        if(person is null) return false;
        if(account.VerifyPassword(password)) throw new ArgumentException("La nueva contraseña debe ser diferente de la anterior. Solicite un código nuevo.");
        account.ChangePassword(password); account.FailedAttempts=0; account.LockedUntilTicks=0;
        var now=clock.GetUtcNow();
        var audit=SensitiveDecision.Begin(person.OrganizationId.Value,"account.password.recover","AccessAccount",person.Id.ToString(),person.Id,now);
        audit.Authorize(person.Id,$"email-recovery:{verification}","verified-contact-self-service-v1",now);
        audit.MarkExecuted($"password-reset:{verification}",now);
        db.SensitiveDecisions.Add(audit);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return true;
    }
}
