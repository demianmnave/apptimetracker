namespace AppTimeTracker.Models;

/// <summary>
/// Represents a health check record persisted in the database for historical tracking and auditing.
/// </summary>
public class HealthCheck
{
    /// <summary>
    /// Unique identifier for the health check record.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Current health status at the time of the check.
    /// </summary>
    public HealthStatus Status { get; set; }

    /// <summary>
    /// Whether the database is connected and accessible during this check.
    /// </summary>
    public bool DatabaseConnected { get; set; }

    /// <summary>
    /// Whether the focus/session monitoring hook is active during this check.
    /// </summary>
    public bool HookActive { get; set; }

    /// <summary>
    /// Memory usage in MB at the time of the check.
    /// </summary>
    public long MemoryUsageMB { get; set; }

    /// <summary>
    /// Timestamp when this health check was performed (UTC).
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Detailed status information and diagnostic message (JSON format).
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// Number of pending events in the telemetry buffer at check time.
    /// </summary>
    public int PendingEventCount { get; set; }

    /// <summary>
    /// Service uptime in seconds at the time of the check.
    /// </summary>
    public long UptimeSeconds { get; set; }

    /// <summary>
    /// Last focus event timestamp if available.
    /// </summary>
    public DateTime? LastEventTime { get; set; }

    /// <summary>
    /// Indication of whether automatic recovery was triggered.
    /// </summary>
    public bool RecoveryTriggered { get; set; }
}
