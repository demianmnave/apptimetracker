using AppTimeTracker.Data;
using AppTimeTracker.Models;

namespace AppTimeTracker;

/// <summary>
/// Background worker service that monitors application focus and tracks usage time.
/// Implements graceful shutdown with session persistence.
/// </summary>
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceProvider _serviceProvider;

    // Current active session being tracked (null if no app has focus)
    private AppUsageSession? _currentSession;
    private readonly object _sessionLock = new();

    public Worker(ILogger<Worker> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AppTimeTracker Worker started at: {Time}", DateTimeOffset.Now);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // Placeholder: Focus monitoring will be implemented in a future milestone
                // For now, just keep the service running and responsive to shutdown
                await Task.Delay(1000, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful shutdown requested
            _logger.LogInformation("Shutdown requested, stopping worker...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Worker encountered an unexpected error");
            throw;
        }
    }

    /// <summary>
    /// Called when the service is stopping. Persists any active session to the database.
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("AppTimeTracker Worker stopping at: {Time}", DateTimeOffset.Now);

        try
        {
            // Persist any active session before shutdown
            await PersistCurrentSessionAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error persisting session during shutdown");
        }

        await base.StopAsync(cancellationToken);
        _logger.LogInformation("AppTimeTracker Worker stopped successfully");
    }

    /// <summary>
    /// Saves the current tracking session to the database if one exists.
    /// </summary>
    private async Task PersistCurrentSessionAsync(CancellationToken cancellationToken)
    {
        AppUsageSession? sessionToSave;

        lock (_sessionLock)
        {
            if (_currentSession == null)
            {
                _logger.LogDebug("No active session to persist");
                return;
            }

            // End the current session
            _currentSession.EndTimeUtc = DateTime.UtcNow;
            _currentSession.CalculateDuration();
            sessionToSave = _currentSession;
            _currentSession = null;
        }

        if (sessionToSave != null)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Check if session already exists (was previously saved)
                if (sessionToSave.Id > 0)
                {
                    dbContext.AppUsageSessions.Update(sessionToSave);
                }
                else
                {
                    dbContext.AppUsageSessions.Add(sessionToSave);
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation(
                    "Persisted session for {ProcessName}, Duration: {Duration}s",
                    sessionToSave.ProcessName,
                    sessionToSave.DurationSeconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist session for {ProcessName}", sessionToSave.ProcessName);
                throw;
            }
        }
    }

    /// <summary>
    /// Starts tracking a new application session.
    /// Called when focus changes to a new application (will be implemented in future milestone).
    /// </summary>
    protected void StartNewSession(string processName, string? executablePath, string? windowTitle, string userId)
    {
        lock (_sessionLock)
        {
            _currentSession = new AppUsageSession
            {
                ProcessName = processName,
                ExecutablePath = executablePath,
                WindowTitle = windowTitle,
                StartTimeUtc = DateTime.UtcNow,
                SessionDate = DateOnly.FromDateTime(DateTime.UtcNow),
                UserId = userId
            };
        }

        _logger.LogDebug("Started tracking session for {ProcessName}", processName);
    }

    /// <summary>
    /// Ends the current tracking session and saves it to the database.
    /// Called when focus changes away from the current application (will be implemented in future milestone).
    /// </summary>
    protected async Task EndCurrentSessionAsync(CancellationToken cancellationToken)
    {
        await PersistCurrentSessionAsync(cancellationToken);
    }
}
