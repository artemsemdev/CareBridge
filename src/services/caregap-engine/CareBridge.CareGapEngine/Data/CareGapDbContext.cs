using CareBridge.CareGapEngine.Entities;
using Microsoft.EntityFrameworkCore;

namespace CareBridge.CareGapEngine.Data;

public class CareGapDbContext : DbContext
{
    public CareGapDbContext(DbContextOptions<CareGapDbContext> options) : base(options)
    {
    }

    public DbSet<Alert> Alerts => Set<Alert>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Alert>(entity =>
        {
            entity.ToTable("Alerts");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Type).HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.Severity).HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(1000).IsRequired();
            entity.Property(e => e.AcknowledgedBy).HasMaxLength(200);
            entity.Property(e => e.ResolvedBy).HasMaxLength(200);
            entity.HasIndex(e => e.CaseId).HasDatabaseName("IX_Alerts_CaseId");
            entity.HasIndex(e => e.Status).HasDatabaseName("IX_Alerts_Status");
            entity.HasIndex(e => e.Severity).HasDatabaseName("IX_Alerts_Severity");
        });
    }
}
