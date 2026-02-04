using AppTimeTracker.Configuration;
using AppTimeTracker.Services.HealthMonitors;
using AppTimeTracker.Data;
using AppTimeTracker.Models;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text.Json;

namespace AppTimeTracker.Services;

/// <summary>
/// Background service that performs periodic health checks on core service components.
/// Aggregates results from multiple health monitors, persists results to database, and triggers recovery when needed.
/// </summary>
public class HealthCheckService : BackgroundService
{
    private readonly ILogger<HealthCheckService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly HealthCheckSettings _settings;
    private readonly IRecoveryManager _recoveryManager;
    private readonly IHealthCheckRepository? _healthCheckRepository;
    private readonly ILoggingService? _loggingService;
    private readonly Stopwatch _uptime;

    public HealthCheckService(
        ILogger<HealthCheckService> logger,
        IServiceProvider serviceProvider,
        IOptions<HealthCheckSettings> options,
        IRecoveryManager recoveryManager,
        IHealthCheckRepository? healthCheckRepository = null,
        ILoggingService? loggingService = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _settings = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _recoveryManager = recoveryManager ?? throw new ArgumentNullException(nameof(recoveryManager));
        _healthCheckRepository = healthCheckRepository;
        _loggingService = loggingService;
        _uptime = Stopwatch.StartNew();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("HealthCheckService is disabled in configuration");
            return;
        }

        _logger.LogInformation("HealthCheckService started with {IntervalSeconds}s interval", _settings.IntervalSeconds);

        var interval = TimeSpan.FromSeconds(_settings.IntervalSeconds);

        try
        {
            // Wait for initial startup
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PerformHealthCheckAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Graceful shutdown
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during health check cycle");
                }

                // Wait for next interval
                await Task.Delay(interval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("HealthCheckService shutdown requested");
        }
        finally
        {
            _logger.LogInformation("HealthCheckService stopped");
        }
    }

    /// <summary>
    /// Performs a complete health check cycle across all registered monitors.
    /// Persists results to database and triggers recovery if needed.
    /// </summary>
    private async Task PerformHealthCheckAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Starting health check cycle");

        // Get all registered health monitors from DI
        var monitors = GetHealthMonitors();

        var results = new List<HealthCheckResult>();
        var overallStatus = HealthStatus.Healthy;
        var databaseConnected = false;
        var hookActive = false;
        var memoryUsageMB = 0L;
        var pendingEventCount = 0;
        var lastEventTime = DateTime.MinValue;
        var recoveryTriggered = false;

        foreach (var monitor in monitors)
        {
            try
            {
                _logger.LogDebug("Probing {MonitorName}...", monitor.Name);
                var result = await monitor.ProbeAsync(cancellationToken);
                results.Add(result);

                // Extract metrics from results
                if (result.DatabaseConnected) databaseConnected = true;
                if (result.HookActive) hookActive = true;
                memoryUsageMB = Math.Max(memoryUsageMB, result.MemoryUsageMB);
                pendingEventCount = Math.Max(pendingEventCount, result.PendingEventCount);
                if (result.LastEventTime.HasValue && result.LastEventTime.Value > lastEventTime)
                {
                    lastEventTime = result.LastEventTime.Value;
                }

                // Update overall status (worst status wins)
                if (result.Status > overallStatus)
                {
                    overallStatus = result.Status;
                }

                // Trigger recovery if component is unhealthy or degraded
                if (result.Status != HealthStatus.Healthy)
                {
                    _logger.LogInformation("Triggering recovery for {ComponentName}", monitor.Name);
                    var recovered = await _recoveryManager.TryRecoverAsync(monitor, result, cancellationToken);

                    if (recovered)
                    {
                        _logger.LogInformation("Recovery succeeded for {ComponentName}, re-running probe", monitor.Name);
                        var reProbeResult = await monitor.ProbeAsync(cancellationToken);
                        if (reProbeResult.Status == HealthStatus.Healthy)
                        {
                            _logger.LogInformation("Post-recovery probe confirms healthy for {ComponentName}", monitor.Name);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Recovery failed for {ComponentName}", monitor.Name);
                        recoveryTriggered = true;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error probing {MonitorName}", monitor.Name);
                var errorResult = new HealthCheckResult(
                    monitor.Name,
                    HealthStatus.Unhealthy,
                    $"Health check failed: {ex.Message}")
                {
                    Exception = ex
                };
                results.Add(errorResult);
                overallStatus = HealthStatus.Unhealthy;
            }
        }

        // Log overall health summary
        LogHealthSummary(results, overallStatus);

        // Persist health check result to database (non-blocking)
        if (_healthCheckRepository != null)
        {
            try
            {
                var healthCheck = new HealthCheck
                {
                    Status = overallStatus,
                    DatabaseConnected = databaseConnected,
                    HookActive = hookActive,
                    MemoryUsageMB = memoryUsageMB,
                    PendingEventCount = pendingEventCount,
                    LastEventTime = lastEventTime == DateTime.MinValue ? null : lastEventTime,
                    UptimeSeconds = (long)_uptime.Elapsed.TotalSeconds,
                    RecoveryTriggered = recoveryTriggered,
                    Details = TrySerializeResultsToJson(results),
                    Timestamp = DateTime.UtcNow
                };

                // Fire and forget - don't block health checks on persistence
                _ = _healthCheckRepository.RecordHealthCheckAsync(healthCheck, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist health check result to database");
            }
        }

        // Log events if health status has degraded
        if (overallStatus != HealthStatus.Healthy && _loggingService != null)
        {
            var severity = overallStatus == HealthStatus.Degraded ? LogSeverity.Warning : LogSeverity.Error;
            var details = JsonSerializer.Serialize(new
            {
                OverallStatus = overallStatus,
                DatabaseConnected = databaseConnected,
                HookActive = hookActive,
                MemoryUsageMB = memoryUsageMB,
                RecoveryTriggered = recoveryTriggered
            });

            _ = _loggingService.PersistLogAsync(
                severity,
                $"Health check failed: {overallStatus}",
                "HealthCheckService",
                Environment.UserName,
                details,
                null,
                null,
                null,
                cancellationToken);
        }
    }

    /// <summary>
    /// Retrieves all registered health monitors from dependency injection.
    /// </summary>
    private IReadOnlyList<IHealthMonitor> GetHealthMonitors()
    {
        var monitors = new List<IHealthMonitor>();

        try
        {
            // Try to get registered monitors
            var sqliteMonitor = _serviceProvider.GetService(typeof(SqliteHealthMonitor)) as IHealthMonitor;
            if (sqliteMonitor != null) monitors.Add(sqliteMonitor);

            var winEventHookMonitor = _serviceProvider.GetService(typeof(WinEventHookHealthMonitor)) as IHealthMonitor;
            if (winEventHookMonitor != null) monitors.Add(winEventHookMonitor);

            var memoryMonitor = _serviceProvider.GetService(typeof(MemoryHealthMonitor)) as IHealthMonitor;
            if (memoryMonitor != null) monitors.Add(memoryMonitor);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving health monitors from DI");
        }

        if (monitors.Count == 0)
        {
            _logger.LogWarning("No health monitors registered in DI container");
        }

        return monitors.AsReadOnly();
    }

    /// <summary>
    /// Logs a summary of the overall health status.
    /// </summary>
    private void LogHealthSummary(List<HealthCheckResult> results, HealthStatus overallStatus)
    {
        if (results.Count == 0)
        {
            _logger.LogWarning("No health check results to summarize");
            return;
        }

        var healthyCount = results.Count(r => r.Status == HealthStatus.Healthy);
        var degradedCount = results.Count(r => r.Status == HealthStatus.Degraded);
        var unhealthyCount = results.Count(r => r.Status == HealthStatus.Unhealthy);

        _logger.LogInformation(
            "Health Check Summary - Overall: {OverallStatus} | Healthy: {HealthyCount} | Degraded: {DegradedCount} | Unhealthy: {UnhealthyCount}",
            overallStatus, healthyCount, degradedCount, unhealthyCount);

        // Log details of any non-healthy components
        foreach (var result in results.Where(r => r.Status != HealthStatus.Healthy))
        {
            _logger.LogWarning(
                "{ComponentName} - Status: {Status}, Description: {Description}",
                result.ComponentName, result.Status, result.Description);
        }
    }

    /// <summary>
    /// Attempts to serialize health check results to JSON string.
    /// </summary>
    private static string? TrySerializeResultsToJson(List<HealthCheckResult> results)
    {
        if (results == null || results.Count == 0)
        {
            return null;
        }

        try
        {
            var summary = new
            {
                CheckCount = results.Count,
                Results = results.Select(r => new
                {
                    r.ComponentName,
                    Status = r.Status.ToString(),
                    r.Description,
                    r.DatabaseConnected,
                    r.HookActive,
                    r.MemoryUsageMB,
                    r.PendingEventCount
                }).ToList()
            };

            return JsonSerializer.Serialize(summary);
        }
        catch
        {
            return null;
        }
    }
}
