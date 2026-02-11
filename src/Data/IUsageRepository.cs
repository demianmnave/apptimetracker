using AppTimeTracker.Models;

namespace AppTimeTracker.Data;

/// <summary>
/// Repository interface for app usage session data operations.
/// Provides methods to save, query, and aggregate usage sessions with atomic transaction support.
/// </summary>
public interface IUsageRepository
{
    /// <summary>
    /// Saves a new app usage session to the database with atomic transaction.
    /// </summary>
    /// <param name="session">The session to save.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>The saved session with generated Id.</returns>
    Task<AppUsageSession> SaveSessionAsync(AppUsageSession session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing app usage session (e.g., setting EndTimeUtc and DurationSeconds).
    /// </summary>
    /// <param name="session">The session to update.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>The updated session.</returns>
    Task<AppUsageSession> UpdateSessionAsync(AppUsageSession session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all app usage sessions within an optional date range, filtered by user.
    /// </summary>
    /// <param name="startDate">Optional start date (inclusive).</param>
    /// <param name="endDate">Optional end date (inclusive).</param>
    /// <param name="userId">Optional user ID filter.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>List of matching sessions.</returns>
    Task<List<AppUsageSession>> GetSessionsByDateRangeAsync(
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        string? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves aggregated total duration by process name for a date range.
    /// </summary>
    /// <param name="startDate">Optional start date (inclusive).</param>
    /// <param name="endDate">Optional end date (inclusive).</param>
    /// <param name="userId">Optional user ID filter.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>Dictionary mapping process names to total duration in seconds.</returns>
    Task<Dictionary<string, long>> GetAggregatedDurationByProcessAsync(
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        string? userId = null,
        CancellationToken cancellationToken = default);
}
