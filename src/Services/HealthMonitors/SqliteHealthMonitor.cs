using AppTimeTracker.Data;

namespace AppTimeTracker.Services.HealthMonitors;

/// <summary>
/// Health monitor for SQLite database connectivity and responsiveness.
/// </summary>
public class SqliteHealthMonitor : IHealthMonitor
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<SqliteHealthMonitor> _logger;
    private const int HealthCheckTimeoutSeconds = 5;

    public string Name => "SQLite";

    public SqliteHealthMonitor(AppDbContext dbContext, ILogger<SqliteHealthMonitor> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Probes SQLite database with a simple test query within timeout.
    /// </summary>
    public async Task<HealthCheckResult> ProbeAsync(CancellationToken cancellationToken)
    {
        var result = new HealthCheckResult(Name, HealthStatus.Healthy, "Database connection is healthy");

        try
        {
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                cts.CancelAfter(TimeSpan.FromSeconds(HealthCheckTimeoutSeconds));

                // Execute a simple test query to verify connectivity
                var canConnect = await _dbContext.Database.CanConnectAsync(cts.Token);

                if (canConnect)
                {
                    result.Status = HealthStatus.Healthy;
                    result.Description = "Database connection successful";
                    result.Data!["ConnectionTime"] = DateTime.UtcNow;
                    _logger.LogDebug("SQLite health check passed");
                }
                else
                {
                    result.Status = HealthStatus.Unhealthy;
                    result.Description = "Cannot connect to database";
                    _logger.LogWarning("SQLite health check failed: Cannot connect");
                }
            }
        }
        catch (OperationCanceledException ex)
        {
            result.Status = HealthStatus.Degraded;
            result.Description = $"Database query timeout ({HealthCheckTimeoutSeconds}s)";
            result.Exception = ex;
            _logger.LogWarning(ex, "SQLite health check timed out");
        }
        catch (Exception ex)
        {
            result.Status = HealthStatus.Unhealthy;
            result.Description = $"Database connection failed: {ex.Message}";
            result.Exception = ex;
            _logger.LogError(ex, "SQLite health check failed with exception");
        }

        return result;
    }

    /// <summary>
    /// Attempts to recover database connection by reconnecting.
    /// </summary>
    public async Task<bool> TryRecoverAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Attempting to recover SQLite connection...");

            // Try to reconnect by disposing and recreating connection
            await _dbContext.Database.CloseConnectionAsync();
            await Task.Delay(100, cancellationToken);

            var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
            if (canConnect)
            {
                _logger.LogInformation("SQLite recovery successful");
                return true;
            }

            _logger.LogWarning("SQLite recovery failed: still cannot connect");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SQLite recovery failed with exception");
            return false;
        }
    }
}
