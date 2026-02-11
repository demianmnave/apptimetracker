namespace AppTimeTracker.Services;

/// <summary>
/// Utility class for formatting time durations.
/// Provides consistent human-readable formatting across application (e.g., "2h 30m 45s").
/// </summary>
public static class DurationFormatter
{
    /// <summary>
    /// Formats duration in seconds to human-readable format.
    /// </summary>
    /// <param name="seconds">Duration in seconds.</param>
    /// <returns>Formatted duration string (e.g., "2h 30m 45s", "5m 30s", "45s").</returns>
    public static string Format(long seconds)
    {
        if (seconds < 0)
        {
            throw new ArgumentException("Duration cannot be negative.", nameof(seconds));
        }

        var hours = seconds / 3600;
        var minutes = (seconds % 3600) / 60;
        var secs = seconds % 60;

        if (hours > 0)
        {
            return $"{hours}h {minutes}m {secs}s";
        }
        else if (minutes > 0)
        {
            return $"{minutes}m {secs}s";
        }
        else
        {
            return $"{secs}s";
        }
    }
}
