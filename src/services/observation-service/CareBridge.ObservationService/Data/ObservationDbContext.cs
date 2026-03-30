using CareBridge.ObservationService.Entities;
using Microsoft.EntityFrameworkCore;

namespace CareBridge.ObservationService.Data;

public class ObservationDbContext : DbContext
{
    public ObservationDbContext(DbContextOptions<ObservationDbContext> options) : base(options)
    {
    }

    public DbSet<Observation> Observations => Set<Observation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Observation>(entity =>
        {
            entity.ToTable("Observations");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Type).HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.Unit).HasMaxLength(50).IsRequired();
            entity.Property(e => e.IdempotencyKey).HasMaxLength(200).IsRequired();
            entity.Property(e => e.DeviceId).HasMaxLength(100);
            entity.Property(e => e.Value).HasPrecision(18, 4);
            entity.HasIndex(e => new { e.CaseId, e.RecordedAt }).HasDatabaseName("IX_Observations_CaseId_RecordedAt");
            entity.HasIndex(e => e.IdempotencyKey).IsUnique().HasDatabaseName("IX_Observations_IdempotencyKey");
        });
    }
}
