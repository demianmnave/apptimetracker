namespace AppTimeTracker.Services.HealthMonitors;

/// <summary>
/// Health monitor for Windows Event Hook (foreground window focus tracking).
/// </summary>
public class WinEventHookHealthMonitor : IHealthMonitor
{
    private readonly IFocusMonitorService _focusMonitor;
    private readonly ILogger<WinEventHookHealthMonitor> _logger;
    private DateTime _lastEventReceived = DateTime.UtcNow;

    public string Name => "WinEventHook";

    public WinEventHookHealthMonitor(IFocusMonitorService focusMonitor, ILogger<WinEventHookHealthMonitor> logger)
    {
        _focusMonitor = focusMonitor ?? throw new ArgumentNullException(nameof(focusMonitor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Subscribe to focus changes to track event reception
        _focusMonitor.FocusChanged += OnFocusChanged;
    }

    /// <summary>
    /// Probes WinEventHook to verify it's active and receiving events.
    /// </summary>
    public async Task<HealthCheckResult> ProbeAsync(CancellationToken cancellationToken)
    {
        var result = new HealthCheckResult(Name, HealthStatus.Healthy, "WinEventHook is active");

        try
        {
            // Check if focus monitor is active
            var isActive = _focusMonitor.IsHookActive;

            if (!isActive)
            {
                result.Status = HealthStatus.Unhealthy;
                result.Description = "WinEventHook is not active";
                _logger.LogWarning("WinEventHook health check failed: hook not active");
                return result;
            }

            // Check if events have been received recently (within last 5 minutes)
            var timeSinceLastEvent = DateTime.UtcNow - _lastEventReceived;
            if (timeSinceLastEvent > TimeSpan.FromMinutes(5))
            {
                result.Status = HealthStatus.Degraded;
                result.Description = $"No focus events received for {timeSinceLastEvent.TotalSeconds:F0} seconds";
                result.Data!["LastEventReceivedAt"] = _lastEventReceived;
                result.Data!["TimeSinceLastEvent"] = timeSinceLastEvent.TotalSeconds;
                _logger.LogWarning("WinEventHook health check: no recent events");
            }
            else
            {
                result.Status = HealthStatus.Healthy;
                result.Description = "WinEventHook is active and receiving events";
                result.Data!["LastEventReceivedAt"] = _lastEventReceived;
                result.Data!["TimeSinceLastEvent"] = timeSinceLastEvent.TotalSeconds;
                _logger.LogDebug("WinEventHook health check passed");
            }

            return result;
        }
        catch (Exception ex)
        {
            result.Status = HealthStatus.Unhealthy;
            result.Description = $"WinEventHook health check failed: {ex.Message}";
            result.Exception = ex;
            _logger.LogError(ex, "WinEventHook health check failed with exception");
            return result;
        }
    }

    /// <summary>
    /// Attempts to recover WinEventHook by stopping and restarting the focus monitor.
    /// </summary>
    public async Task<bool> TryRecoverAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Attempting to recover WinEventHook...");

            // Stop and restart the focus monitor to re-register the hook
            await _focusMonitor.StopAsync(cancellationToken);
            await Task.Delay(200, cancellationToken);

            await _focusMonitor.StartAsync(cancellationToken);

            if (_focusMonitor.IsHookActive)
            {
                _logger.LogInformation("WinEventHook recovery successful");
                _lastEventReceived = DateTime.UtcNow;
                return true;
            }

            _logger.LogWarning("WinEventHook recovery failed: hook still not active");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WinEventHook recovery failed with exception");
            return false;
        }
    }

    /// <summary>
    /// Called when a focus change event is received.
    /// </summary>
    private void OnFocusChanged(object? sender, FocusChangeEventArgs? e)
    {
        _lastEventReceived = DateTime.UtcNow;
    }
}
