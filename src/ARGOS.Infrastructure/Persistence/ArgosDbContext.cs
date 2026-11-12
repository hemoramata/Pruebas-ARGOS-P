using ARGOS.Domain.Aggregates.CaseFile;
using ARGOS.Domain.Aggregates.Evidence;
using ARGOS.Domain.Aggregates.Organization;
using ARGOS.Domain.Aggregates.OrganizationSetup;
using ARGOS.Domain.Aggregates.Procedure;
using Microsoft.EntityFrameworkCore;

namespace ARGOS.Infrastructure.Persistence;

public sealed class ArgosDbContext : DbContext
{
    public ArgosDbContext(DbContextOptions<ArgosDbContext> options)
        : base(options)
    {
    }

    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<OrganizationSetup> OrganizationSetups => Set<OrganizationSetup>();

    public DbSet<Procedure> Procedures => Set<Procedure>();
    public DbSet<ARGOS.Domain.Aggregates.Workflow.Workflow> Workflows => Set<ARGOS.Domain.Aggregates.Workflow.Workflow>();

    public DbSet<CaseFile> CaseFiles => Set<CaseFile>();

    public DbSet<Evidence> Evidences => Set<Evidence>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<ProtectedCaseContent> ProtectedCaseContents => Set<ProtectedCaseContent>();
    public DbSet<InstitutionalOperationPolicy> OperationPolicies => Set<InstitutionalOperationPolicy>();
    public DbSet<DocumentCategory> DocumentCategories => Set<DocumentCategory>();
    public DbSet<DocumentClassificationReview> DocumentClassificationReviews => Set<DocumentClassificationReview>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ArgosDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
        var content = modelBuilder.Entity<ProtectedCaseContent>();
        content.ToTable("ProtectedCaseContents");
        content.HasKey(x => x.Id);
        content.Property(x => x.Id).ValueGeneratedNever();
        content.Property(x => x.ContentType).HasMaxLength(200).IsRequired();
        content.Property(x => x.Ciphertext).IsRequired();
        content.Property(x => x.KeyVersion).HasMaxLength(100).IsRequired();
        content.Property(x => x.Algorithm).HasMaxLength(100).IsRequired();
        content.Property(x => x.AssociatedData).HasMaxLength(500).IsRequired();
        content.HasIndex(x => x.CaseFileId);
        var policy = modelBuilder.Entity<InstitutionalOperationPolicy>();
        policy.ToTable("InstitutionalOperationPolicies");
        policy.HasKey(x => x.Id);
        policy.Property(x => x.Id).ValueGeneratedNever();
        policy.Property(x => x.ProcedureCode).HasMaxLength(50).IsRequired();
        policy.Property(x => x.RulesJson).IsRequired();
        policy.Ignore(x => x.Rules);
        policy.HasIndex(x => new { x.OrganizationId, x.ProcedureCode, x.Version }).IsUnique();
        policy.HasOne<ProtectedCaseContent>().WithMany().HasForeignKey(x => x.ProofContentId).OnDelete(DeleteBehavior.Restrict);
        var category = modelBuilder.Entity<DocumentCategory>();
        category.ToTable("DocumentCategories");
        category.HasKey(x => x.Id);
        category.Property(x => x.Id).ValueGeneratedNever();
        category.Property(x => x.Code).HasMaxLength(50).IsRequired();
        category.Property(x => x.Name).HasMaxLength(150).IsRequired();
        category.Property(x => x.Description).HasMaxLength(1000);
        category.HasIndex(x => new { x.OrganizationId, x.Code }).IsUnique();
        var review = modelBuilder.Entity<DocumentClassificationReview>();
        review.ToTable("DocumentClassificationReviews");
        review.HasKey(x => x.Id);
        review.Property(x => x.Id).ValueGeneratedNever();
        review.Property(x => x.SuggestionSource).HasMaxLength(80).IsRequired();
        review.Property(x => x.Decision).HasMaxLength(30).IsRequired();
        review.HasIndex(x => x.ProtectedContentId).IsUnique();
        review.HasIndex(x => new { x.OrganizationId, x.FinalCategoryId });
    }
}
