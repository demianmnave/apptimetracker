namespace AppTimeTracker.Data;

/// <summary>
/// Data transfer object for aggregated usage summary by process.
/// Provides a typed alternative to Dictionary for returning aggregation query results.
/// </summary>
public class UsageSummaryDto
{
    /// <summary>
    /// Name of the process.
    /// </summary>
    public string ProcessName { get; set; } = string.Empty;

    /// <summary>
    /// Total duration of all sessions for this process in seconds.
    /// </summary>
    public long TotalDurationSeconds { get; set; }
}
