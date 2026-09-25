using LearnForge.Core;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LearnForge.Api;

public class AppDb(DbContextOptions options) : IdentityDbContext<User>(options)
{
    public DbSet<PackRelease> Packs => Set<PackRelease>();
    public DbSet<Attempt> Attempts => Set<Attempt>();
    public DbSet<ResponseEvent> Responses => Set<ResponseEvent>();
    public DbSet<LessonProgress> LessonProgress => Set<LessonProgress>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<EvidenceRecord> Evidence => Set<EvidenceRecord>();
    public DbSet<AuditEvent> Audit => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<PackRelease>().HasIndex(x => new { x.PackId, x.Version }).IsUnique();
        builder.Entity<Attempt>().HasIndex(x => new { x.UserId, x.StartKey }).IsUnique();
        builder.Entity<Attempt>().HasIndex(x => x.ActiveKey).IsUnique();
        builder.Entity<Attempt>().HasIndex(x => new { x.UserId, x.PackId, x.StartedAt });
        builder.Entity<Attempt>().Property(x => x.Mode).HasConversion<string>();
        builder.Entity<Attempt>().Property(x => x.Size).HasConversion<string>();
        builder.Entity<Attempt>().Property(x => x.Status).HasConversion<string>();
        builder.Entity<Attempt>().Property(x => x.Focus).HasConversion<string>();
        builder.Entity<Attempt>().Property(x => x.Revision).IsConcurrencyToken();
        builder.Entity<Attempt>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Attempt>().HasOne<PackRelease>().WithMany().HasForeignKey(x => x.PackReleaseId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<ResponseEvent>().HasIndex(x => new { x.AttemptId, x.RequestId }).IsUnique();
        builder.Entity<ResponseEvent>().HasOne<Attempt>().WithMany().HasForeignKey(x => x.AttemptId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<LessonProgress>().HasKey(x => new { x.UserId, x.PackId, x.LessonId });
        builder.Entity<LessonProgress>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Enrollment>().HasKey(x => new { x.UserId, x.PackId });
        builder.Entity<Enrollment>().Property(x => x.Status).HasConversion<string>();
        builder.Entity<Enrollment>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        // The unique index makes ledger writes idempotent per attempt and question.
        builder.Entity<EvidenceRecord>().HasIndex(x => new { x.AttemptId, x.QuestionId }).IsUnique();
        builder.Entity<EvidenceRecord>().HasIndex(x => new { x.UserId, x.PackId, x.At });
        builder.Entity<EvidenceRecord>().Property(x => x.Source).HasConversion<string>();
        builder.Entity<EvidenceRecord>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<EvidenceRecord>().HasOne<Attempt>().WithMany().HasForeignKey(x => x.AttemptId).OnDelete(DeleteBehavior.Cascade);
    }
}
