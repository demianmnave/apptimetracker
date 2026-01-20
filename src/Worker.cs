using AppTimeTracker.Data;
using AppTimeTracker.Models;
using AppTimeTracker.Services;

namespace AppTimeTracker;

/// <summary>
/// Background worker service that monitors application focus and tracks usage time.
/// Integrates with SessionMonitorService to pause tracking during lock/disconnect events.
/// Implements graceful shutdown with session persistence.
/// </summary>
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ISessionMonitorService _sessionMonitor;
    private readonly IFocusMonitorService _focusMonitor;
    private readonly IConfiguration _configuration;

    // Current active session being tracked (null if no app has focus)
    private AppUsageSession? _currentSession;
    private readonly object _sessionLock = new();

    // Tracking pause state
    private bool _isTrackingPaused;

    // Minimum session duration filter (1 second)
    private const int MinSessionDurationSeconds = 1;

    public Worker(
        ILogger<Worker> logger,
        IServiceScopeFactory serviceScopeFactory,
        ISessionMonitorService sessionMonitor,
        IFocusMonitorService focusMonitor,
        IConfiguration configuration)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
        _sessionMonitor = sessionMonitor;
        _focusMonitor = focusMonitor;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        _logger.LogInformation("AppTimeTracker Worker started - Version: {Version}, Time: {Time}, User: {UserId}, Session: {State}",
            version ?? new System.Version(0, 0, 0, 0),
            DateTimeOffset.Now,
            _sessionMonitor.CurrentUserId ?? "Unknown",
            _sessionMonitor.CurrentState);

        // Subscribe to session state changes
        _sessionMonitor.SessionStateChanged += OnSessionStateChanged;

        // Subscribe to focus change events
        _focusMonitor.FocusChanged += OnFocusChanged;

        try
        {
            // Set initial tracking state based on session
            _isTrackingPaused = !_sessionMonitor.IsSessionActive;

            while (!stoppingToken.IsCancellationRequested)
            {
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
            _logger.LogError(ex, "Worker encountered an unexpected error - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                ex.GetType().FullName, ex.Message, ex.StackTrace);
            throw;
        }
        finally
        {
            _sessionMonitor.SessionStateChanged -= OnSessionStateChanged;
            _focusMonitor.FocusChanged -= OnFocusChanged;
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
    /// Uses IUsageRepository for atomic transaction support.
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
                using var scope = _serviceScopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IUsageRepository>();

                // Check if session already exists (was previously saved)
                if (sessionToSave.Id > 0)
                {
                    await repository.UpdateSessionAsync(sessionToSave, cancellationToken);
                }
                else
                {
                    await repository.SaveSessionAsync(sessionToSave, cancellationToken);
                }

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

    /// <summary>
    /// Handles session state changes (lock/unlock/disconnect/reconnect).
    /// Pauses tracking when workstation is locked or user switches away.
    /// Resumes tracking when user returns to their session.
    /// </summary>
    private async void OnSessionStateChanged(object? sender, SessionStateChangeEventArgs? e)
    {
        if (e == null)
        {
            _logger.LogWarning("SessionStateChanged event received with null arguments");
            return;
        }

        var pauseOnLock = _configuration.GetSection("SessionMonitoring").GetValue("PauseOnLock", true);

        try
        {
            _logger.LogDebug("Session state changed for user {UserId}: {OldState} -> {NewState}",
                e.UserId, e.OldState, e.NewState);
            switch (e.NewState)
            {
                case SessionState.Active when _isTrackingPaused:
                    // Session unlocked or user returned - resume tracking
                    _logger.LogInformation("Session unlocked/reconnected. Resuming tracking for user: {UserId}", e.UserId);
                    _isTrackingPaused = false;
                    break;

                case SessionState.Locked when pauseOnLock && !_isTrackingPaused:
                    {
                        // Workstation locked - pause and persist current session
                        _logger.LogInformation("Workstation locked. Pausing tracking.");
                        _isTrackingPaused = true;

                        // Persist any active session immediately
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                        await PersistCurrentSessionAsync(cts.Token);
                        break;
                    }

                case SessionState.Disconnected:
                    {
                        // User switched away or logged off - pause and persist
                        _logger.LogInformation("Session disconnected. Pausing tracking.");
                        _isTrackingPaused = true;

                        // Persist any active session immediately
                        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                        await PersistCurrentSessionAsync(cts2.Token);
                        break;
                    }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling session state change");
        }
    }

    /// <summary>
    /// Handles focus changes to a new application window.
    /// Ends the previous session and starts tracking the new focused application.
    /// Ignores focus changes shorter than 1 second.
    /// </summary>
    private void OnFocusChanged(object? sender, FocusChangeEventArgs? e)
    {
        if (e == null)
        {
            _logger.LogWarning("FocusChanged event received with null arguments");
            return;
        }

        // Don't track if tracking is paused (session locked/disconnected)
        if (_isTrackingPaused)
        {
            _logger.LogDebug("Focus change ignored - tracking is paused for {ProcessName}", e.ProcessName);
            return;
        }

        try
        {
            _logger.LogDebug("Focus changed to {ProcessName} (PID: {ProcessId}), Tracking paused: {TrackingPaused}",
                e.ProcessName, e.ProcessId, _isTrackingPaused);
            lock (_sessionLock)
            {
                // If there's a current session, check if it meets minimum duration
                if (_currentSession != null)
                {
                    var sessionDuration = (int)(DateTime.UtcNow - _currentSession.StartTimeUtc).TotalSeconds;

                    // Only persist sessions that lasted at least 1 second
                    if (sessionDuration >= MinSessionDurationSeconds)
                    {
                        _currentSession.EndTimeUtc = DateTime.UtcNow;
                        _currentSession.CalculateDuration();

                        // Persist the session (fire and forget, don't block focus changes)
                        _ = PersistCurrentSessionAsync(CancellationToken.None);
                    }
                    else
                    {
                        _logger.LogDebug(
                            "Ignoring session for {ProcessName} - duration {Duration}s is less than minimum {Min}s",
                            _currentSession.ProcessName,
                            sessionDuration,
                            MinSessionDurationSeconds);
                    }
                }

                // Start tracking the newly focused application
                StartNewSession(e.ProcessName, e.ExecutablePath, e.WindowTitle, _sessionMonitor.CurrentUserId ?? "Unknown");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling focus change");
        }
    }
}
