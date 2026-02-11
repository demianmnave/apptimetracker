namespace AppTimeTracker.Configuration;

/// <summary>
/// Configuration settings for application session tracking and monitoring.
/// </summary>
public class SessionTrackingSettings
{
    /// <summary>
    /// Minimum session duration in seconds before persisting to database.
    /// Sessions shorter than this threshold are discarded (default: 1 second).
    /// </summary>
    public int MinSessionDurationSeconds { get; set; } = 1;

    /// <summary>
    /// Whether to pause tracking when the workstation is locked (default: true).
    /// </summary>
    public bool PauseOnLock { get; set; } = true;

    /// <summary>
    /// Interval in milliseconds for polling application focus changes (default: 500ms).
    /// </summary>
    public int PollingIntervalMs { get; set; } = 500;

    /// <summary>
    /// Interval in milliseconds for flushing sessions to database (default: 30000ms = 30 seconds).
    /// </summary>
    public int SessionFlushIntervalMs { get; set; } = 30_000;

    /// <summary>
    /// Timeout in seconds for graceful shutdown operations (default: 5 seconds).
    /// </summary>
    public int GracefulShutdownTimeoutSeconds { get; set; } = 5;
}
