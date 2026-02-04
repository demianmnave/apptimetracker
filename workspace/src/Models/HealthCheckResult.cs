namespace AppTimeTracker.Models;

/// <summary>
/// Enumeration of possible health statuses.
/// </summary>
public enum HealthStatus
{
    Healthy,
    Degraded,
    Unhealthy
}

/// <summary>
/// DTO for health check results returned to callers.
/// </summary>
public class HealthCheckResult
{
    /// <summary>
    /// Current health status of the application.
    /// </summary>
    public HealthStatus Status { get; set; }

    /// <summary>
    /// Descriptive message about the health status.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Whether the database is connected and accessible.
    /// </summary>
    public bool DatabaseConnected { get; set; }

    /// <summary>
    /// Whether the focus/session monitoring hook is active.
    /// </summary>
    public bool HookActive { get; set; }

    /// <summary>
    /// Current memory usage in MB.
    /// </summary>
    public long MemoryUsageMB { get; set; }

    /// <summary>
    /// Timestamp of the last focus event recorded.
    /// </summary>
    public DateTime? LastEventTime { get; set; }

    /// <summary>
    /// Timestamp when this health check was generated.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Number of pending events in the telemetry buffer.
    /// </summary>
    public int PendingEventCount { get; set; }

    /// <summary>
    /// Uptime of the service in seconds.
    /// </summary>
    public long UptimeSeconds { get; set; }
}
