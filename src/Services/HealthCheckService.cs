using AppTimeTracker.Configuration;
using AppTimeTracker.Services.HealthMonitors;
using Microsoft.Extensions.Options;

namespace AppTimeTracker.Services;

/// <summary>
/// Background service that performs periodic health checks on core service components.
/// Aggregates results from multiple health monitors and triggers recovery when needed.
/// </summary>
public class HealthCheckService : BackgroundService
{
    private readonly ILogger<HealthCheckService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly HealthCheckSettings _settings;
    private readonly IRecoveryManager _recoveryManager;

    public HealthCheckService(
        ILogger<HealthCheckService> logger,
        IServiceProvider serviceProvider,
        IOptions<HealthCheckSettings> options,
        IRecoveryManager recoveryManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _settings = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _recoveryManager = recoveryManager ?? throw new ArgumentNullException(nameof(recoveryManager));
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
    /// </summary>
    private async Task PerformHealthCheckAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Starting health check cycle");

        // Get all registered health monitors from DI
        var monitors = GetHealthMonitors();

        var results = new List<HealthCheckResult>();
        var overallStatus = HealthStatus.Healthy;

        foreach (var monitor in monitors)
        {
            try
            {
                _logger.LogDebug("Probing {MonitorName}...", monitor.Name);
                var result = await monitor.ProbeAsync(cancellationToken);
                results.Add(result);

                // Update overall status (worst status wins)
                if (result.Status > overallStatus)
                {
                    overallStatus = result.Status;
                }

                _logger.LogInformation(
                    "Health check result for {ComponentName}: {Status} - {Description}",
                    result.ComponentName, result.Status, result.Description);

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
}
