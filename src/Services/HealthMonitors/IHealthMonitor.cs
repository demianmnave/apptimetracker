namespace AppTimeTracker.Services.HealthMonitors;

/// <summary>
/// Represents the status of a health check component.
/// </summary>
public enum HealthStatus
{
    /// <summary>
    /// Component is functioning normally.
    /// </summary>
    Healthy,

    /// <summary>
    /// Component is functioning but with degraded performance or non-critical issues.
    /// </summary>
    Degraded,

    /// <summary>
    /// Component is not functioning and requires attention.
    /// </summary>
    Unhealthy
}

/// <summary>
/// Result of a health check for a component.
/// </summary>
public class HealthCheckResult
{
    /// <summary>
    /// Name of the component being monitored.
    /// </summary>
    public string ComponentName { get; set; }

    /// <summary>
    /// Current health status.
    /// </summary>
    public HealthStatus Status { get; set; }

    /// <summary>
    /// Detailed description of the health status.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Timestamp of the health check.
    /// </summary>
    public DateTime CheckedAt { get; set; }

    /// <summary>
    /// Exception details if check failed (optional).
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Additional data about the component state.
    /// </summary>
    public Dictionary<string, object>? Data { get; set; }

    public HealthCheckResult(string componentName, HealthStatus status, string description)
    {
        ComponentName = componentName;
        Status = status;
        Description = description;
        CheckedAt = DateTime.UtcNow;
        Data = new Dictionary<string, object>();
    }
}

/// <summary>
/// Interface for a health monitor component that checks service health.
/// </summary>
public interface IHealthMonitor
{
    /// <summary>
    /// Name of the monitor (e.g., "SQLite", "WinEventHook", "Memory").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Probes the component and returns health status.
    /// </summary>
    Task<HealthCheckResult> ProbeAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Attempts to recover the component if it's unhealthy.
    /// Returns true if recovery was successful.
    /// </summary>
    Task<bool> TryRecoverAsync(CancellationToken cancellationToken);
}
