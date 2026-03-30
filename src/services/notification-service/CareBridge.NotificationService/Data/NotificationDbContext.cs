using CareBridge.NotificationService.Entities;
using Microsoft.EntityFrameworkCore;

namespace CareBridge.NotificationService.Data;

public class NotificationDbContext : DbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options)
    {
    }

    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("Notifications");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Channel).HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.Recipient).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Subject).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Body).HasMaxLength(4000).IsRequired();
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.TriggeredBy).HasMaxLength(200).IsRequired();

            entity.HasIndex(e => e.CaseId).HasDatabaseName("IX_Notifications_CaseId");
            entity.HasIndex(e => e.CreatedAt).HasDatabaseName("IX_Notifications_CreatedAt");
        });
    }
}
