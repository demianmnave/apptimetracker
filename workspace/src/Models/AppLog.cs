namespace AppTimeTracker.Models;

/// <summary>
/// Enumeration of log severity levels.
/// </summary>
public enum LogSeverity
{
    Debug = 0,
    Info = 1,
    Warning = 2,
    Error = 3,
    Critical = 4
}

/// <summary>
/// Represents a log entry stored in the database with full context and metadata.
/// </summary>
public class AppLog
{
    /// <summary>
    /// Unique identifier for the log entry.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Severity level of the log entry.
    /// </summary>
    public LogSeverity Severity { get; set; }

    /// <summary>
    /// Log message content.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Structured context data as JSON string for rich querying.
    /// </summary>
    public string? ContextData { get; set; }

    /// <summary>
    /// Timestamp when the log entry was created (UTC).
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Source component that generated the log (e.g., "SessionMonitor", "FocusDetection").
    /// </summary>
    public string SourceComponent { get; set; } = string.Empty;

    /// <summary>
    /// User ID or system identifier associated with the log entry.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Correlation ID for tracing related log entries across requests/operations.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Exception stack trace if this log represents an error.
    /// </summary>
    public string? StackTrace { get; set; }

    /// <summary>
    /// Additional metadata stored as JSON for extensibility.
    /// </summary>
    public string? AdditionalMetadata { get; set; }
}
