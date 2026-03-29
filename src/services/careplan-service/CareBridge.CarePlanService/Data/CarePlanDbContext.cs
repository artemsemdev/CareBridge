using CareBridge.CarePlanService.Entities;
using Microsoft.EntityFrameworkCore;

namespace CareBridge.CarePlanService.Data;

public class CarePlanDbContext : DbContext
{
    public CarePlanDbContext(DbContextOptions<CarePlanDbContext> options) : base(options)
    {
    }

    public DbSet<CarePlanEntity> CarePlans => Set<CarePlanEntity>();
    public DbSet<MilestoneEntity> Milestones => Set<MilestoneEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CarePlanEntity>(entity =>
        {
            entity.ToTable("CarePlans");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TemplateName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(50);
            entity.HasIndex(e => e.CaseId).IsUnique().HasDatabaseName("IX_CarePlans_CaseId");
            entity.HasMany(e => e.Milestones)
                  .WithOne(m => m.CarePlan)
                  .HasForeignKey(m => m.CarePlanId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MilestoneEntity>(entity =>
        {
            entity.ToTable("Milestones");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(1000).IsRequired();
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(50);
            entity.HasIndex(e => e.CarePlanId).HasDatabaseName("IX_Milestones_CarePlanId");
        });
    }
}
