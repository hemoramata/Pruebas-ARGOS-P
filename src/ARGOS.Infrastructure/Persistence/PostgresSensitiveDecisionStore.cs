using ARGOS.Application.Security;
using Microsoft.EntityFrameworkCore;
namespace ARGOS.Infrastructure.Persistence;
public sealed class PostgresSensitiveDecisionStore(AccessDbContext db) : ISensitiveDecisionStore
{
 public Task AddAsync(SensitiveDecision decision,CancellationToken ct=default){db.SensitiveDecisions.Add(decision);return Task.CompletedTask;}
 public Task<SensitiveDecision?> GetAsync(Guid organizationId,Guid id,CancellationToken ct=default)=>db.SensitiveDecisions.SingleOrDefaultAsync(d=>d.OrganizationId==organizationId&&d.Id==id,ct);
 public Task SaveAsync(CancellationToken ct=default)=>db.SaveChangesAsync(ct);
}
