using AppTimeTracker.Services.HealthMonitors;
using Microsoft.Extensions.Options;
using AppTimeTracker.Configuration;

namespace AppTimeTracker.Services;

/// <summary>
/// Interface for automatic recovery management of failed health checks.
/// </summary>
public interface IRecoveryManager
{
    /// <summary>
    /// Attempts to recover a failed health monitor with retry logic.
    /// </summary>
    Task<bool> TryRecoverAsync(IHealthMonitor monitor, HealthCheckResult failedResult, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the current failure count for a specific monitor.
    /// </summary>
    int GetFailureCount(string monitorName);

    /// <summary>
    /// Resets the failure count for a specific monitor.
    /// </summary>
    void ResetFailureCount(string monitorName);
}

/// <summary>
/// Manages automatic recovery attempts for failed health monitors with exponential backoff.
/// </summary>
public class RecoveryManager : IRecoveryManager
{
    private readonly ILogger<RecoveryManager> _logger;
    private readonly HealthCheckSettings _settings;
    private readonly Dictionary<string, int> _failureCount = new();
    private readonly object _lockObject = new();

    public RecoveryManager(ILogger<RecoveryManager> logger, IOptions<HealthCheckSettings> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settings = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Attempts to recover a failed health monitor with exponential backoff retry logic.
    /// </summary>
    public async Task<bool> TryRecoverAsync(IHealthMonitor monitor, HealthCheckResult failedResult, CancellationToken cancellationToken)
    {
        if (!_settings.EnableAutoRecovery)
        {
            _logger.LogInformation("Auto-recovery is disabled, skipping recovery for {ComponentName}", monitor.Name);
            return false;
        }

        var failureCount = GetFailureCount(monitor.Name);

        if (failureCount >= _settings.RetryCount)
        {
            _logger.LogError("Recovery failed for {ComponentName}: exceeded maximum retry count ({Count})",
                monitor.Name, _settings.RetryCount);
            return false;
        }

        try
        {
            _logger.LogInformation("Starting recovery attempt {Attempt}/{MaxAttempts} for {ComponentName}",
                failureCount + 1, _settings.RetryCount, monitor.Name);

            // Calculate exponential backoff delay
            var backoffMs = _settings.InitialBackoffMs * Math.Pow(_settings.BackoffMultiplier, failureCount);
            var delayMs = (int)Math.Min(backoffMs, 30000); // Cap at 30 seconds

            _logger.LogDebug("Waiting {DelayMs}ms before recovery attempt", delayMs);
            await Task.Delay(delayMs, cancellationToken);

            // Attempt recovery
            _logger.LogInformation("Executing recovery procedure for {ComponentName}", monitor.Name);
            var recoverySucceeded = await monitor.TryRecoverAsync(cancellationToken);

            if (recoverySucceeded)
            {
                _logger.LogInformation("Recovery succeeded for {ComponentName}, resetting failure count", monitor.Name);
                ResetFailureCount(monitor.Name);

                // Re-probe to confirm recovery
                var probeResult = await monitor.ProbeAsync(cancellationToken);
                if (probeResult.Status == HealthStatus.Healthy)
                {
                    _logger.LogInformation("Post-recovery probe confirmed healthy status for {ComponentName}", monitor.Name);
                    return true;
                }
                else
                {
                    _logger.LogWarning("Post-recovery probe shows {Status} status for {ComponentName}",
                        probeResult.Status, monitor.Name);
                    IncrementFailureCount(monitor.Name);
                    return false;
                }
            }
            else
            {
                _logger.LogWarning("Recovery procedure failed for {ComponentName}, incrementing failure count", monitor.Name);
                IncrementFailureCount(monitor.Name);
                return false;
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Recovery attempt cancelled for {ComponentName}", monitor.Name);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Recovery attempt failed with exception for {ComponentName}", monitor.Name);
            IncrementFailureCount(monitor.Name);
            return false;
        }
    }

    /// <summary>
    /// Gets the current failure count for a specific monitor.
    /// </summary>
    public int GetFailureCount(string monitorName)
    {
        lock (_lockObject)
        {
            return _failureCount.TryGetValue(monitorName, out var count) ? count : 0;
        }
    }

    /// <summary>
    /// Resets the failure count for a specific monitor.
    /// </summary>
    public void ResetFailureCount(string monitorName)
    {
        lock (_lockObject)
        {
            _failureCount[monitorName] = 0;
            _logger.LogDebug("Reset failure count for {ComponentName}", monitorName);
        }
    }

    /// <summary>
    /// Increments the failure count for a specific monitor.
    /// </summary>
    private void IncrementFailureCount(string monitorName)
    {
        lock (_lockObject)
        {
            var currentCount = GetFailureCount(monitorName);
            _failureCount[monitorName] = currentCount + 1;
            _logger.LogDebug("Incremented failure count for {ComponentName} to {Count}",
                monitorName, currentCount + 1);
        }
    }
}
