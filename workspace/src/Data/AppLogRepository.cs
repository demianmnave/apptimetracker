using Microsoft.EntityFrameworkCore;
using AppTimeTracker.Models;

namespace AppTimeTracker.Data;

/// <summary>
/// Interface for app log persistence operations.
/// </summary>
public interface IAppLogRepository
{
    /// <summary>
    /// Creates and persists a new log entry to the database.
    /// </summary>
    Task<AppLog> CreateLogAsync(AppLog log, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a log entry for required fields and valid severity.
    /// </summary>
    /// <param name="log">The log entry to validate.</param>
    /// <returns>A tuple of (isValid, errorMessage).</returns>
    (bool IsValid, string? ErrorMessage) ValidateLog(AppLog log);

    /// <summary>
    /// Retrieves logs within a date range, optionally filtered by severity.
    /// </summary>
    Task<List<AppLog>> GetLogsAsync(DateTime startDate, DateTime endDate, LogSeverity? severity = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves logs filtered by severity level.
    /// </summary>
    Task<List<AppLog>> GetLogsBySeverityAsync(LogSeverity severity, int limit = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves logs by source component.
    /// </summary>
    Task<List<AppLog>> GetLogsByComponentAsync(string sourceComponent, int limit = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves logs by correlation ID for tracing related events.
    /// </summary>
    Task<List<AppLog>> GetLogsByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes log entries older than 30 days to maintain database size.
    /// </summary>
    Task<int> DeleteOldLogsAsync(int retentionDays = 30, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the most recent log entry.
    /// </summary>
    Task<AppLog?> GetLatestLogAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets total count of log entries in the database.
    /// </summary>
    Task<int> GetLogCountAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of app log repository with database persistence.
/// </summary>
public class AppLogRepository : IAppLogRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<AppLogRepository> _logger;

    public AppLogRepository(AppDbContext context, ILogger<AppLogRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Validates a log entry for required fields and valid severity.
    /// </summary>
    public (bool IsValid, string? ErrorMessage) ValidateLog(AppLog log)
    {
        if (log == null)
        {
            return (false, "Log entry cannot be null.");
        }

        if (string.IsNullOrWhiteSpace(log.Message))
        {
            return (false, "Log message cannot be empty.");
        }

        if (log.Message.Length > 2000)
        {
            return (false, "Log message exceeds maximum length of 2000 characters.");
        }

        // Validate severity is within valid range
        if (!Enum.IsDefined(typeof(LogSeverity), log.Severity))
        {
            return (false, $"Invalid severity level: {log.Severity}. Must be one of: Debug, Info, Warning, Error, Critical.");
        }

        if (string.IsNullOrWhiteSpace(log.SourceComponent))
        {
            return (false, "Source component cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(log.UserId))
        {
            return (false, "User ID cannot be empty.");
        }

        return (true, null);
    }

    /// <summary>
    /// Creates and persists a new log entry to the database.
    /// </summary>
    public async Task<AppLog> CreateLogAsync(AppLog log, CancellationToken cancellationToken = default)
    {
        var (isValid, errorMessage) = ValidateLog(log);
        if (!isValid)
        {
            throw new ArgumentException($"Log validation failed: {errorMessage}");
        }

        log.Timestamp = DateTime.UtcNow;

        _context.AppLogs.Add(log);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Log entry created with ID {LogId}: {Message}", log.Id, log.Message);

        return log;
    }

    /// <summary>
    /// Retrieves logs within a date range, optionally filtered by severity.
    /// </summary>
    public async Task<List<AppLog>> GetLogsAsync(DateTime startDate, DateTime endDate, LogSeverity? severity = null, CancellationToken cancellationToken = default)
    {
        var query = _context.AppLogs
            .Where(l => l.Timestamp >= startDate && l.Timestamp <= endDate)
            .OrderByDescending(l => l.Timestamp);

        if (severity.HasValue)
        {
            query = query.Where(l => l.Severity == severity.Value);
        }

        return await query.ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Retrieves logs filtered by severity level.
    /// </summary>
    public async Task<List<AppLog>> GetLogsBySeverityAsync(LogSeverity severity, int limit = 100, CancellationToken cancellationToken = default)
    {
        return await _context.AppLogs
            .Where(l => l.Severity == severity)
            .OrderByDescending(l => l.Timestamp)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Retrieves logs by source component.
    /// </summary>
    public async Task<List<AppLog>> GetLogsByComponentAsync(string sourceComponent, int limit = 100, CancellationToken cancellationToken = default)
    {
        return await _context.AppLogs
            .Where(l => l.SourceComponent == sourceComponent)
            .OrderByDescending(l => l.Timestamp)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Retrieves logs by correlation ID for tracing related events.
    /// </summary>
    public async Task<List<AppLog>> GetLogsByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default)
    {
        return await _context.AppLogs
            .Where(l => l.CorrelationId == correlationId)
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Deletes log entries older than 30 days (or specified retention days).
    /// </summary>
    public async Task<int> DeleteOldLogsAsync(int retentionDays = 30, CancellationToken cancellationToken = default)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);

        var logsToDelete = await _context.AppLogs
            .Where(l => l.Timestamp < cutoffDate)
            .ToListAsync(cancellationToken);

        if (logsToDelete.Count == 0)
        {
            return 0;
        }

        _context.AppLogs.RemoveRange(logsToDelete);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted {Count} log entries older than {Days} days.", logsToDelete.Count, retentionDays);

        return logsToDelete.Count;
    }

    /// <summary>
    /// Gets the most recent log entry.
    /// </summary>
    public async Task<AppLog?> GetLatestLogAsync(CancellationToken cancellationToken = default)
    {
        return await _context.AppLogs
            .OrderByDescending(l => l.Timestamp)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Gets total count of log entries in the database.
    /// </summary>
    public async Task<int> GetLogCountAsync(CancellationToken cancellationToken = default)
    {
        return await _context.AppLogs.CountAsync(cancellationToken);
    }
}
