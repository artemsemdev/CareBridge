using CareBridge.CaseService.Entities;
using Microsoft.EntityFrameworkCore;

namespace CareBridge.CaseService.Data;

public class CaseDbContext : DbContext
{
    public CaseDbContext(DbContextOptions<CaseDbContext> options) : base(options)
    {
    }

    public DbSet<CaseEntity> Cases => Set<CaseEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CaseEntity>(entity =>
        {
            entity.ToTable("Cases");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PatientName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.DiagnosisCode).HasMaxLength(20).IsRequired();
            entity.Property(e => e.DiagnosisDescription).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(50);
            entity.HasIndex(e => e.PatientId);
            entity.HasIndex(e => e.Status);
        });
    }
}
