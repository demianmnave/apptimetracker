using Microsoft.EntityFrameworkCore;
using AppTimeTracker.Models;

namespace AppTimeTracker.Data;

/// <summary>
/// Repository for app usage session data with atomic transaction support.
/// Handles CRUD operations and aggregation queries for usage tracking data.
/// </summary>
public class UsageRepository : IUsageRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<UsageRepository> _logger;

    public UsageRepository(AppDbContext context, ILogger<UsageRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Saves a new app usage session to the database with atomic transaction.
    /// </summary>
    public async Task<AppUsageSession> SaveSessionAsync(AppUsageSession session, CancellationToken cancellationToken = default)
    {
        using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            _context.AppUsageSessions.Add(session);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogDebug(
                "Saved session for {ProcessName}, Duration: {Duration}s, SessionId: {SessionId}",
                session.ProcessName,
                session.DurationSeconds,
                session.Id);

            return session;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to save session for {ProcessName}, rolling back transaction", session.ProcessName);
            throw;
        }
    }

    /// <summary>
    /// Updates an existing app usage session with atomic transaction.
    /// </summary>
    public async Task<AppUsageSession> UpdateSessionAsync(AppUsageSession session, CancellationToken cancellationToken = default)
    {
        using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            _context.AppUsageSessions.Update(session);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogDebug(
                "Updated session for {ProcessName}, Duration: {Duration}s, SessionId: {SessionId}",
                session.ProcessName,
                session.DurationSeconds,
                session.Id);

            return session;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to update session for {ProcessName}, rolling back transaction", session.ProcessName);
            throw;
        }
    }

    /// <summary>
    /// Retrieves all app usage sessions within an optional date range, filtered by user.
    /// Uses indexed SessionDate column for efficient queries.
    /// </summary>
    public async Task<List<AppUsageSession>> GetSessionsByDateRangeAsync(
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AppUsageSessions.AsNoTracking();

        if (startDate.HasValue)
        {
            query = query.Where(s => s.SessionDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(s => s.SessionDate <= endDate.Value);
        }

        if (!string.IsNullOrEmpty(userId))
        {
            query = query.Where(s => s.UserId == userId);
        }

        var sessions = await query
            .OrderByDescending(s => s.StartTimeUtc)
            .ToListAsync(cancellationToken);

        _logger.LogDebug(
            "Retrieved {Count} sessions for date range {StartDate} to {EndDate}, User: {UserId}",
            sessions.Count,
            startDate,
            endDate,
            userId ?? "All");

        return sessions;
    }

    /// <summary>
    /// Retrieves aggregated total duration by process name for a date range.
    /// Uses indexed ProcessName and SessionDate columns for efficient grouping.
    /// </summary>
    public async Task<Dictionary<string, long>> GetAggregatedDurationByProcessAsync(
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AppUsageSessions.AsNoTracking();

        if (startDate.HasValue)
        {
            query = query.Where(s => s.SessionDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(s => s.SessionDate <= endDate.Value);
        }

        if (!string.IsNullOrEmpty(userId))
        {
            query = query.Where(s => s.UserId == userId);
        }

        var aggregation = await query
            .GroupBy(s => s.ProcessName)
            .Select(g => new { ProcessName = g.Key, TotalDuration = g.Sum(s => s.DurationSeconds) })
            .ToListAsync(cancellationToken);

        var result = aggregation.ToDictionary(x => x.ProcessName, x => x.TotalDuration);

        _logger.LogDebug(
            "Retrieved aggregated durations for {ProcessCount} processes, Date range: {StartDate} to {EndDate}, User: {UserId}",
            result.Count,
            startDate,
            endDate,
            userId ?? "All");

        return result;
    }
}
