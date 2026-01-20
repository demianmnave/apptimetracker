namespace AppTimeTracker.Services.HealthMonitors;

/// <summary>
/// Health monitor for service memory usage.
/// </summary>
public class MemoryHealthMonitor : IHealthMonitor
{
    private readonly ILogger<MemoryHealthMonitor> _logger;
    private readonly int _memoryThresholdMB;

    public string Name => "Memory";

    public MemoryHealthMonitor(ILogger<MemoryHealthMonitor> logger, int memoryThresholdMB = 100)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _memoryThresholdMB = memoryThresholdMB;
    }

    /// <summary>
    /// Probes memory usage and returns status based on configurable threshold.
    /// </summary>
    public async Task<HealthCheckResult> ProbeAsync(CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            var result = new HealthCheckResult(Name, HealthStatus.Healthy, "Memory usage is normal");

            try
            {
                // Get current memory usage
                var process = System.Diagnostics.Process.GetCurrentProcess();
                var memoryMB = process.WorkingSet64 / (1024 * 1024);
                var gcTotalMemoryMB = GC.GetTotalMemory(false) / (1024 * 1024);

                result.Data!["WorkingSetMB"] = memoryMB;
                result.Data!["GCTotalMemoryMB"] = gcTotalMemoryMB;
                result.Data!["ThresholdMB"] = _memoryThresholdMB;

                // Calculate threshold percentages
                var usagePercent = (double)memoryMB / _memoryThresholdMB * 100;

                if (usagePercent >= 100)
                {
                    result.Status = HealthStatus.Unhealthy;
                    result.Description = $"Memory usage {memoryMB}MB exceeds threshold {_memoryThresholdMB}MB";
                    _logger.LogError("Memory health check failed: usage {MemoryMB}MB exceeds threshold", memoryMB);
                }
                else if (usagePercent >= 80)
                {
                    result.Status = HealthStatus.Degraded;
                    result.Description = $"Memory usage {memoryMB}MB is at {usagePercent:F1}% of threshold";
                    _logger.LogWarning("Memory health check: usage at {Percent:F1}% of threshold", usagePercent);
                }
                else
                {
                    result.Status = HealthStatus.Healthy;
                    result.Description = $"Memory usage {memoryMB}MB is normal ({usagePercent:F1}% of threshold)";
                    _logger.LogDebug("Memory health check passed: {MemoryMB}MB", memoryMB);
                }

                result.Data!["UsagePercent"] = usagePercent;
                return result;
            }
            catch (Exception ex)
            {
                result.Status = HealthStatus.Unhealthy;
                result.Description = $"Memory check failed: {ex.Message}";
                result.Exception = ex;
                _logger.LogError(ex, "Memory health check failed with exception");
                return result;
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Attempts to recover by forcing garbage collection to reduce memory usage.
    /// </summary>
    public async Task<bool> TryRecoverAsync(CancellationToken cancellationToken)
    {
        return await Task.Run(async () =>
        {
            try
            {
                _logger.LogInformation("Attempting memory recovery via garbage collection...");

                // Force garbage collection
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                await Task.Delay(100, cancellationToken);

                var process = System.Diagnostics.Process.GetCurrentProcess();
                var memoryMB = process.WorkingSet64 / (1024 * 1024);

                _logger.LogInformation("Memory recovery completed. Current usage: {MemoryMB}MB", memoryMB);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Memory recovery failed with exception");
                return false;
            }
        }, cancellationToken);
    }
}
