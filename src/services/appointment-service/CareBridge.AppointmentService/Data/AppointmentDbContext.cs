using CareBridge.AppointmentService.Entities;
using Microsoft.EntityFrameworkCore;

namespace CareBridge.AppointmentService.Data;

public class AppointmentDbContext : DbContext
{
    public AppointmentDbContext(DbContextOptions<AppointmentDbContext> options) : base(options)
    {
    }

    public DbSet<Appointment> Appointments => Set<Appointment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Appointment>(entity =>
        {
            entity.ToTable("Appointments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Type).HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.Notes).HasMaxLength(1000);

            entity.HasIndex(e => e.CaseId).HasDatabaseName("IX_Appointments_CaseId");
            entity.HasIndex(e => e.Status).HasDatabaseName("IX_Appointments_Status");
            entity.HasIndex(e => e.ScheduledAt).HasDatabaseName("IX_Appointments_ScheduledAt");
        });
    }
}
