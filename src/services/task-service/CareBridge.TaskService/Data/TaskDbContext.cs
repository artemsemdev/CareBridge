using CareBridge.TaskService.Entities;
using Microsoft.EntityFrameworkCore;

namespace CareBridge.TaskService.Data;

public class TaskDbContext : DbContext
{
    public TaskDbContext(DbContextOptions<TaskDbContext> options) : base(options)
    {
    }

    public DbSet<CareTask> CareTasks => Set<CareTask>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CareTask>(entity =>
        {
            entity.ToTable("CareTasks");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(2000).IsRequired();
            entity.Property(e => e.AssignedTo).HasMaxLength(100);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.Priority).HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.CompletedBy).HasMaxLength(100);

            entity.HasIndex(e => e.CaseId).HasDatabaseName("IX_Tasks_CaseId");
            entity.HasIndex(e => e.Status).HasDatabaseName("IX_Tasks_Status");
            entity.HasIndex(e => e.AssignedTo).HasDatabaseName("IX_Tasks_AssignedTo");
            entity.HasIndex(e => e.AlertId)
                  .IsUnique()
                  .HasFilter("[AlertId] IS NOT NULL")
                  .HasDatabaseName("IX_Tasks_AlertId");
        });
    }
}
