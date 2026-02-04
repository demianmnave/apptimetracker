using AppTimeTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace AppTimeTracker.Data;

/// <summary>
/// Interface for FocusEvent repository operations.
/// </summary>
public interface IFocusEventRepository
{
    /// <summary>
    /// Gets all events for a specific date and user.
    /// </summary>
    Task<List<FocusEvent>> GetEventsByDateAndUserAsync(DateOnly date, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets events within a date range.
    /// </summary>
    Task<List<FocusEvent>> GetEventsByDateRangeAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets events for a specific process.
    /// </summary>
    Task<List<FocusEvent>> GetEventsByProcessAsync(string processName, DateOnly? date = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if an event with the given correlation ID already exists.
    /// </summary>
    Task<bool> EventExistsByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the most recent event for a user.
    /// </summary>
    Task<FocusEvent?> GetMostRecentEventAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all events for a specific user.
    /// </summary>
    Task<List<FocusEvent>> GetEventsByUserAsync(string userId, DateOnly? startDate = null, DateOnly? endDate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts events in a date range.
    /// </summary>
    Task<int> CountEventsByDateRangeAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository for CRUD operations on FocusEvent.
/// Includes query methods for events by date range, process name, with deduplication checks.
/// </summary>
public class FocusEventRepository : IFocusEventRepository
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<FocusEventRepository> _logger;

    public FocusEventRepository(AppDbContext dbContext, ILogger<FocusEventRepository> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets all events for a specific date and user.
    /// </summary>
    public async Task<List<FocusEvent>> GetEventsByDateAndUserAsync(DateOnly date, string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var events = await _dbContext.FocusEvents
                .Where(e => e.EventDate == date && e.UserId == userId)
                .OrderBy(e => e.StartTimeUtc)
                .ToListAsync(cancellationToken);

            _logger.LogDebug("Retrieved {EventCount} events for user {UserId} on {Date}", events.Count, userId, date);
            return events;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving events for user {UserId} on {Date}", userId, date);
            throw;
        }
    }

    /// <summary>
    /// Gets events within a date range.
    /// </summary>
    public async Task<List<FocusEvent>> GetEventsByDateRangeAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        try
        {
            var events = await _dbContext.FocusEvents
                .Where(e => e.EventDate >= startDate && e.EventDate <= endDate)
                .OrderBy(e => e.StartTimeUtc)
                .ToListAsync(cancellationToken);

            _logger.LogDebug("Retrieved {EventCount} events between {StartDate} and {EndDate}", events.Count, startDate, endDate);
            return events;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving events between {StartDate} and {EndDate}", startDate, endDate);
            throw;
        }
    }

    /// <summary>
    /// Gets events for a specific process.
    /// </summary>
    public async Task<List<FocusEvent>> GetEventsByProcessAsync(string processName, DateOnly? date = null, CancellationToken cancellationToken = default)
    {
        try
        {
            IQueryable<FocusEvent> query = _dbContext.FocusEvents
                .Where(e => e.ProcessName == processName);

            if (date.HasValue)
            {
                query = query.Where(e => e.EventDate == date.Value);
            }

            var events = await query
                .OrderBy(e => e.StartTimeUtc)
                .ToListAsync(cancellationToken);

            _logger.LogDebug("Retrieved {EventCount} events for process {ProcessName}", events.Count, processName);
            return events;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving events for process {ProcessName}", processName);
            throw;
        }
    }

    /// <summary>
    /// Checks if an event with the given correlation ID already exists.
    /// </summary>
    public async Task<bool> EventExistsByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(correlationId))
            {
                return false;
            }

            var exists = await _dbContext.FocusEvents
                .AnyAsync(e => e.CorrelationId == correlationId, cancellationToken);

            return exists;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking event existence for correlation ID {CorrelationId}", correlationId);
            throw;
        }
    }

    /// <summary>
    /// Gets the most recent event for a user.
    /// </summary>
    public async Task<FocusEvent?> GetMostRecentEventAsync(string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var mostRecentEvent = await _dbContext.FocusEvents
                .Where(e => e.UserId == userId)
                .OrderByDescending(e => e.StartTimeUtc)
                .FirstOrDefaultAsync(cancellationToken);

            return mostRecentEvent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving most recent event for user {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Gets all events for a specific user.
    /// </summary>
    public async Task<List<FocusEvent>> GetEventsByUserAsync(string userId, DateOnly? startDate = null, DateOnly? endDate = null, CancellationToken cancellationToken = default)
    {
        try
        {
            IQueryable<FocusEvent> query = _dbContext.FocusEvents.Where(e => e.UserId == userId);

            if (startDate.HasValue)
            {
                query = query.Where(e => e.EventDate >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(e => e.EventDate <= endDate.Value);
            }

            var events = await query
                .OrderBy(e => e.StartTimeUtc)
                .ToListAsync(cancellationToken);

            _logger.LogDebug("Retrieved {EventCount} events for user {UserId}", events.Count, userId);
            return events;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving events for user {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Counts events in a date range.
    /// </summary>
    public async Task<int> CountEventsByDateRangeAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        try
        {
            var count = await _dbContext.FocusEvents
                .Where(e => e.EventDate >= startDate && e.EventDate <= endDate)
                .CountAsync(cancellationToken);

            _logger.LogDebug("Event count between {StartDate} and {EndDate}: {Count}", startDate, endDate, count);
            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error counting events between {StartDate} and {EndDate}", startDate, endDate);
            throw;
        }
    }
}
