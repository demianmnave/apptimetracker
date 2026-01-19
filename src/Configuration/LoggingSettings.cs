namespace AppTimeTracker.Configuration;

/// <summary>
/// Configuration settings for structured logging with file rotation and EventLog fallback.
/// </summary>
public class LoggingSettings
{
    /// <summary>
    /// Maximum file size in bytes before rolling to a new log file (default: 10MB).
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 10_000_000;

    /// <summary>
    /// Number of log files to retain (default: 5).
    /// </summary>
    public int RetainedFileCount { get; set; } = 5;

    /// <summary>
    /// Enable Windows Event Log fallback if file logging fails (default: true).
    /// </summary>
    public bool EventLogFallbackEnabled { get; set; } = true;

    /// <summary>
    /// Minimum log level (default: Information).
    /// </summary>
    public string MinimumLevel { get; set; } = "Information";

    /// <summary>
    /// Log file directory path (default: %ProgramData%\AppTimeTracker\Logs).
    /// </summary>
    public string? LogFilePath { get; set; }
}
