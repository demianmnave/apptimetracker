namespace AppTimeTracker.Configuration;

/// <summary>
/// Configuration settings for automated health monitoring and recovery.
/// </summary>
public class HealthCheckSettings
{
    /// <summary>
    /// Interval in seconds between health checks (default: 60).
    /// </summary>
    public int IntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Memory usage threshold in MB before warning (default: 100).
    /// </summary>
    public int MemoryThresholdMB { get; set; } = 100;

    /// <summary>
    /// Number of retry attempts for failed health checks (default: 3).
    /// </summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// Exponential backoff multiplier for retries (default: 2).
    /// </summary>
    public double BackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Initial backoff delay in milliseconds before first retry (default: 100).
    /// </summary>
    public int InitialBackoffMs { get; set; } = 100;

    /// <summary>
    /// Enable automatic recovery attempts (default: true).
    /// </summary>
    public bool EnableAutoRecovery { get; set; } = true;

    /// <summary>
    /// Enable health check service (default: true).
    /// </summary>
    public bool Enabled { get; set; } = true;
}
