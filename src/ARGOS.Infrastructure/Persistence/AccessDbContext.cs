using Microsoft.EntityFrameworkCore;
using ARGOS.Application.Security;

namespace ARGOS.Infrastructure.Persistence;

public sealed class AccessDbContext(DbContextOptions<AccessDbContext> options) : DbContext(options)
{
    public DbSet<AccessAccount> Accounts => Set<AccessAccount>();
    public DbSet<PositionPermission> PositionPermissions => Set<PositionPermission>();
    public DbSet<SensitiveDecision> SensitiveDecisions => Set<SensitiveDecision>();
    public DbSet<SuperuserAccount> Superusers => Set<SuperuserAccount>();
    public DbSet<InstitutionalAuthority> Authorities => Set<InstitutionalAuthority>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var account = modelBuilder.Entity<AccessAccount>();
        account.ToTable("AccessAccounts");
        account.HasKey(a => a.PersonId);
        account.Property(a => a.PersonId).ValueGeneratedNever();
        account.HasIndex(a => a.Identification).IsUnique();
        account.Property(a => a.Identification).IsRequired().HasMaxLength(100);
        account.Property(a => a.Salt).IsRequired();
        account.Property(a => a.PasswordDigest).IsRequired();
        account.Ignore(a => a.MaskedDestination);
        var permission = modelBuilder.Entity<PositionPermission>();
        permission.ToTable("AccessPositionPermissions");
        permission.HasKey(p => new { p.OrganizationId, p.PositionId, p.Permission });
        permission.Property(p => p.Permission).HasMaxLength(100);
        var decision = modelBuilder.Entity<SensitiveDecision>();
        decision.ToTable("SensitiveDecisions");
        decision.HasKey(d => d.Id);
        decision.Property(d => d.Id).ValueGeneratedNever();
        decision.Property(d => d.OperationCode).HasMaxLength(100).IsRequired();
        decision.Property(d => d.SubjectType).HasMaxLength(100).IsRequired();
        decision.Property(d => d.SubjectReference).HasMaxLength(200).IsRequired();
        decision.Property(d => d.VerificationReference).HasMaxLength(200);
        decision.Property(d => d.AuthorizationReference).HasMaxLength(200);
        decision.Property(d => d.ExecutionReference).HasMaxLength(200);
        decision.Property(d => d.RejectionReference).HasMaxLength(200);
        decision.HasIndex(d => new { d.OrganizationId, d.Status, d.InitiatedAt });
        var superuser = modelBuilder.Entity<SuperuserAccount>();
        superuser.ToTable("SuperuserAccounts");
        superuser.HasKey(x => x.PersonId);
        superuser.Property(x => x.PersonId).ValueGeneratedNever();
        superuser.Property(x => x.Identification).HasMaxLength(100).IsRequired();
        superuser.HasIndex(x => new { x.OrganizationId, x.Identification }).IsUnique();
        superuser.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        superuser.Property(x => x.ExternalEmail).HasColumnName("ExternalEmail").HasMaxLength(254).IsRequired();
        superuser.Property(x => x.EmailVerificationReference).HasMaxLength(200).IsRequired();
        superuser.Ignore(x => x.AccessLevel);
        var authority = modelBuilder.Entity<InstitutionalAuthority>();
        authority.ToTable("InstitutionalAuthorities");
        authority.HasKey(x => x.Id);
        authority.Property(x => x.Id).ValueGeneratedNever();
        authority.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        authority.Property(x => x.Email).HasMaxLength(254).IsRequired();
        authority.Property(x => x.VerificationReference).HasMaxLength(200).IsRequired();
    }

    public Task InitializeSchemaAsync(CancellationToken ct = default) => Database.MigrateAsync(ct);
}