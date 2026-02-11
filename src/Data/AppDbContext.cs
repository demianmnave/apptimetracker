using Microsoft.EntityFrameworkCore;
using AppTimeTracker.Models;

namespace AppTimeTracker.Data;

/// <summary>
/// Entity Framework Core database context for app usage tracking.
/// </summary>
public class AppDbContext : DbContext
{
    /// <summary>
    /// App usage sessions table.
    /// </summary>
    public DbSet<AppUsageSession> AppUsageSessions { get; set; } = null!;

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// Configures the model and creates indexes for query optimization.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AppUsageSession>(entity =>
        {
            // Table name
            entity.ToTable("AppUsageSessions");

            // Primary key
            entity.HasKey(e => e.Id);

            // Index for date-based queries (daily/weekly/monthly reports)
            entity.HasIndex(e => e.SessionDate)
                .HasDatabaseName("IX_Sessions_Date");

            // Index for process-based queries (usage by app)
            entity.HasIndex(e => e.ProcessName)
                .HasDatabaseName("IX_Sessions_Process");

            // Composite index for common query patterns
            entity.HasIndex(e => new { e.SessionDate, e.UserId })
                .HasDatabaseName("IX_Sessions_Date_User");

            // Property configurations
            entity.Property(e => e.ProcessName)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(e => e.ExecutablePath)
                .HasMaxLength(1024);

            entity.Property(e => e.WindowTitle)
                .HasMaxLength(1024);

            entity.Property(e => e.UserId)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(e => e.StartTimeUtc)
                .IsRequired();

            entity.Property(e => e.SessionDate)
                .IsRequired();
        });
    }
}
