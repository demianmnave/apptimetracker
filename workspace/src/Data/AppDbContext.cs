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

    /// <summary>
    /// Focus events table for telemetry and monitoring.
    /// </summary>
    public DbSet<FocusEvent> FocusEvents { get; set; } = null!;

    /// <summary>
    /// Application log entries table for structured logging and diagnostics.
    /// </summary>
    public DbSet<AppLog> AppLogs { get; set; } = null!;

    /// <summary>
    /// Health check results table for monitoring and alerting.
    /// </summary>
    public DbSet<HealthCheck> HealthChecks { get; set; } = null!;

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

        // Configure FocusEvent entity
        modelBuilder.Entity<FocusEvent>(entity =>
        {
            // Table name
            entity.ToTable("FocusEvents");

            // Primary key
            entity.HasKey(e => e.Id);

            // Index for date-based queries
            entity.HasIndex(e => e.EventDate)
                .HasDatabaseName("IX_FocusEvents_Date");

            // Index for process-based queries
            entity.HasIndex(e => e.ProcessName)
                .HasDatabaseName("IX_FocusEvents_Process");

            // Index for timestamp queries (for event ordering)
            entity.HasIndex(e => e.StartTimeUtc)
                .HasDatabaseName("IX_FocusEvents_StartTime");

            // Composite index for common query patterns
            entity.HasIndex(e => new { e.EventDate, e.UserId })
                .HasDatabaseName("IX_FocusEvents_Date_User");

            // Index for deduplication
            entity.HasIndex(e => e.CorrelationId)
                .HasDatabaseName("IX_FocusEvents_CorrelationId")
                .IsUnique(false);

            // Property configurations
            entity.Property(e => e.ProcessName)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(e => e.WindowTitle)
                .HasMaxLength(1024);

            entity.Property(e => e.EventType)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.UserId)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(e => e.StartTimeUtc)
                .IsRequired();

            entity.Property(e => e.EventDate)
                .IsRequired();

            entity.Property(e => e.CorrelationId)
                .HasMaxLength(256);
        });

        // Configure AppLog entity
        modelBuilder.Entity<AppLog>(entity =>
        {
            // Table name
            entity.ToTable("AppLogs");

            // Primary key
            entity.HasKey(e => e.Id);

            // Index for timestamp queries (for retention cleanup and range queries)
            entity.HasIndex(e => e.Timestamp)
                .HasDatabaseName("IX_AppLogs_Timestamp");

            // Index for severity filtering
            entity.HasIndex(e => e.Severity)
                .HasDatabaseName("IX_AppLogs_Severity");

            // Index for source component queries
            entity.HasIndex(e => e.SourceComponent)
                .HasDatabaseName("IX_AppLogs_SourceComponent");

            // Index for correlation ID tracing
            entity.HasIndex(e => e.CorrelationId)
                .HasDatabaseName("IX_AppLogs_CorrelationId")
                .IsUnique(false);

            // Composite index for common query patterns
            entity.HasIndex(e => new { e.Timestamp, e.Severity })
                .HasDatabaseName("IX_AppLogs_Timestamp_Severity");

            // Property configurations
            entity.Property(e => e.Message)
                .IsRequired()
                .HasMaxLength(2000);

            entity.Property(e => e.SourceComponent)
                .IsRequired()
                .HasMaxLength(128);

            entity.Property(e => e.UserId)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(e => e.CorrelationId)
                .HasMaxLength(256);

            entity.Property(e => e.Timestamp)
                .IsRequired();

            entity.Property(e => e.Severity)
                .IsRequired();

            entity.Property(e => e.ContextData)
                .HasColumnType("TEXT");

            entity.Property(e => e.StackTrace)
                .HasColumnType("TEXT");

            entity.Property(e => e.AdditionalMetadata)
                .HasColumnType("TEXT");
        });

        // Configure HealthCheck entity
        modelBuilder.Entity<HealthCheck>(entity =>
        {
            // Table name
            entity.ToTable("HealthChecks");

            // Primary key
            entity.HasKey(e => e.Id);

            // Index for timestamp queries (for retrieving latest and historical data)
            entity.HasIndex(e => e.Timestamp)
                .HasDatabaseName("IX_HealthChecks_Timestamp")
                .IsDescending();

            // Index for status queries
            entity.HasIndex(e => e.Status)
                .HasDatabaseName("IX_HealthChecks_Status");

            // Composite index for common query patterns
            entity.HasIndex(e => new { e.Timestamp, e.Status })
                .HasDatabaseName("IX_HealthChecks_Timestamp_Status")
                .IsDescending(true, false);

            // Property configurations
            entity.Property(e => e.Timestamp)
                .IsRequired();

            entity.Property(e => e.Status)
                .IsRequired();

            entity.Property(e => e.Details)
                .HasColumnType("TEXT");

            entity.Property(e => e.DatabaseConnected)
                .IsRequired();

            entity.Property(e => e.HookActive)
                .IsRequired();

            entity.Property(e => e.MemoryUsageMB)
                .IsRequired();
        });
    }
}
