using Microsoft.EntityFrameworkCore;
using AppTimeTracker.Models;

namespace AppTimeTracker.Data;

/// <summary>
/// Interface for health check persistence operations.
/// </summary>
public interface IHealthCheckRepository
{
    /// <summary>
    /// Records a new health check result to the database.
    /// </summary>
    Task<HealthCheck> RecordHealthCheckAsync(HealthCheck healthCheck, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the most recent health check result.
    /// </summary>
    Task<HealthCheck?> GetLatestHealthCheckAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves health check history within a date range.
    /// </summary>
    Task<List<HealthCheck>> GetHealthHistoryAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves health checks with a specific status.
    /// </summary>
    Task<List<HealthCheck>> GetHealthChecksByStatusAsync(HealthStatus status, int limit = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the most recent N health checks.
    /// </summary>
    Task<List<HealthCheck>> GetRecentHealthChecksAsync(int count = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes health check records older than the specified number of days.
    /// </summary>
    Task<int> CleanupOldChecksAsync(int retentionDays = 7, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total count of health check records.
    /// </summary>
    Task<int> GetHealthCheckCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets health check statistics for monitoring.
    /// </summary>
    Task<HealthCheckStatistics> GetHealthCheckStatisticsAsync(DateTime? since = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Statistics about health checks for monitoring and reporting.
/// </summary>
public class HealthCheckStatistics
{
    public int TotalChecks { get; set; }
    public int HealthyCount { get; set; }
    public int DegradedCount { get; set; }
    public int UnhealthyCount { get; set; }
    public double AverageMemoryUsageMB { get; set; }
    public DateTime? FirstCheckTime { get; set; }
    public DateTime? LastCheckTime { get; set; }
    public double HealthyPercentage => TotalChecks > 0 ? (HealthyCount / (double)TotalChecks) * 100 : 0;
}

/// <summary>
/// Implementation of health check repository with database persistence.
/// </summary>
public class HealthCheckRepository : IHealthCheckRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<HealthCheckRepository> _logger;

    public HealthCheckRepository(AppDbContext context, ILogger<HealthCheckRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Records a new health check result to the database.
    /// </summary>
    public async Task<HealthCheck> RecordHealthCheckAsync(HealthCheck healthCheck, CancellationToken cancellationToken = default)
    {
        if (healthCheck == null)
        {
            throw new ArgumentNullException(nameof(healthCheck));
        }

        healthCheck.Timestamp = DateTime.UtcNow;

        _context.HealthChecks.Add(healthCheck);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Health check recorded: Status={Status}, Database={DatabaseConnected}, Hook={HookActive}, Memory={MemoryMB}MB",
            healthCheck.Status, healthCheck.DatabaseConnected, healthCheck.HookActive, healthCheck.MemoryUsageMB);

        return healthCheck;
    }

    /// <summary>
    /// Retrieves the most recent health check result.
    /// </summary>
    public async Task<HealthCheck?> GetLatestHealthCheckAsync(CancellationToken cancellationToken = default)
    {
        return await _context.HealthChecks
            .OrderByDescending(h => h.Timestamp)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Retrieves health check history within a date range.
    /// </summary>
    public async Task<List<HealthCheck>> GetHealthHistoryAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        return await _context.HealthChecks
            .Where(h => h.Timestamp >= startDate && h.Timestamp <= endDate)
            .OrderByDescending(h => h.Timestamp)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Retrieves health checks with a specific status.
    /// </summary>
    public async Task<List<HealthCheck>> GetHealthChecksByStatusAsync(HealthStatus status, int limit = 100, CancellationToken cancellationToken = default)
    {
        return await _context.HealthChecks
            .Where(h => h.Status == status)
            .OrderByDescending(h => h.Timestamp)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Retrieves the most recent N health checks.
    /// </summary>
    public async Task<List<HealthCheck>> GetRecentHealthChecksAsync(int count = 10, CancellationToken cancellationToken = default)
    {
        return await _context.HealthChecks
            .OrderByDescending(h => h.Timestamp)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Deletes health check records older than the specified number of days.
    /// </summary>
    public async Task<int> CleanupOldChecksAsync(int retentionDays = 7, CancellationToken cancellationToken = default)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);

        var checksToDelete = await _context.HealthChecks
            .Where(h => h.Timestamp < cutoffDate)
            .ToListAsync(cancellationToken);

        if (checksToDelete.Count == 0)
        {
            return 0;
        }

        _context.HealthChecks.RemoveRange(checksToDelete);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted {Count} health check records older than {Days} days.", checksToDelete.Count, retentionDays);

        return checksToDelete.Count;
    }

    /// <summary>
    /// Gets the total count of health check records.
    /// </summary>
    public async Task<int> GetHealthCheckCountAsync(CancellationToken cancellationToken = default)
    {
        return await _context.HealthChecks.CountAsync(cancellationToken);
    }

    /// <summary>
    /// Gets health check statistics for monitoring and reporting.
    /// </summary>
    public async Task<HealthCheckStatistics> GetHealthCheckStatisticsAsync(DateTime? since = null, CancellationToken cancellationToken = default)
    {
        var query = _context.HealthChecks.AsQueryable();

        if (since.HasValue)
        {
            query = query.Where(h => h.Timestamp >= since.Value);
        }

        var checks = await query.ToListAsync(cancellationToken);

        if (checks.Count == 0)
        {
            return new HealthCheckStatistics();
        }

        return new HealthCheckStatistics
        {
            TotalChecks = checks.Count,
            HealthyCount = checks.Count(h => h.Status == HealthStatus.Healthy),
            DegradedCount = checks.Count(h => h.Status == HealthStatus.Degraded),
            UnhealthyCount = checks.Count(h => h.Status == HealthStatus.Unhealthy),
            AverageMemoryUsageMB = checks.Average(h => h.MemoryUsageMB),
            FirstCheckTime = checks.Min(h => h.Timestamp),
            LastCheckTime = checks.Max(h => h.Timestamp)
        };
    }
}
