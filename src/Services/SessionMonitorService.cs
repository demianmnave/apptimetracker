using System.Runtime.InteropServices;
using AppTimeTracker.Native;

namespace AppTimeTracker.Services;

/// <summary>
/// Monitors Windows session state changes (lock/unlock/disconnect/reconnect).
/// Uses a hidden message window to receive WM_WTSSESSION_CHANGE messages.
/// Tracks the currently signed-in user ID and exposes session state for pause/resume logic.
/// </summary>
public class SessionMonitorService : ISessionMonitorService
{
    private readonly ILogger<SessionMonitorService> _logger;
    private readonly IConfiguration _configuration;
    private MessageWindow? _messageWindow;
    private SessionState _currentState = SessionState.Active;
    private string? _currentUserId;
    private bool _isStarted;

    public event EventHandler<SessionStateChangeEventArgs>? SessionStateChanged;

    public string? CurrentUserId => _currentUserId;
    public SessionState CurrentState => _currentState;
    public bool IsSessionActive => _currentState == SessionState.Active;

    public SessionMonitorService(
        ILogger<SessionMonitorService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _logger.LogWarning("Session monitoring is only available on Windows. Skipping initialization.");
            return;
        }

        var sessionMonitoringConfig = _configuration.GetSection("SessionMonitoring");
        var isEnabled = sessionMonitoringConfig.GetValue("Enabled", true);

        if (!isEnabled)
        {
            _logger.LogInformation("Session monitoring is disabled in configuration");
            return;
        }

        try
        {
            // Query initial user ID
            _currentUserId = SessionInterop.GetSessionString(SessionInterop.WTS_INFO_CLASS.WTSUserName);
            _logger.LogInformation("Session monitor starting. Current user: {UserId}", _currentUserId ?? "Unknown");

            // Create and initialize message window
            _messageWindow = new MessageWindow(_logger);
            _messageWindow.SessionStateChanged += OnSessionStateChanged;

            await Task.Run(() => _messageWindow.CreateWindow(), cancellationToken);
            _isStarted = true;

            _logger.LogInformation("Session monitoring started successfully");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Session monitoring startup was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start session monitoring");
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (!_isStarted)
            return;

        try
        {
            _logger.LogInformation("Session monitoring stopping...");

            if (_messageWindow != null)
            {
                _messageWindow.SessionStateChanged -= OnSessionStateChanged;
                _messageWindow.DestroyWindow();
                _messageWindow.Dispose();
                _messageWindow = null;
            }

            _isStarted = false;
            _logger.LogInformation("Session monitoring stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping session monitoring");
        }

        await Task.CompletedTask;
    }

    private void OnSessionStateChanged(object? sender, SessionStateEventArgs? e)
    {
        if (e == null) return;

        try
        {
            var oldState = _currentState;

            // Map Windows session change reason to our state enum
            var newState = e.Reason switch
            {
                SessionInterop.WTS_SESSION_LOGON or
                SessionInterop.WTS_SESSION_UNLOCK or
                SessionInterop.WTS_CONSOLE_CONNECT
                    => SessionState.Active,

                SessionInterop.WTS_SESSION_LOCK
                    => SessionState.Locked,

                SessionInterop.WTS_SESSION_LOGOFF or
                SessionInterop.WTS_CONSOLE_DISCONNECT
                    => SessionState.Disconnected,

                _ => _currentState
            };

            // Update user ID on relevant events
            if (e.Reason == SessionInterop.WTS_SESSION_LOGON ||
                e.Reason == SessionInterop.WTS_SESSION_UNLOCK)
            {
                _currentUserId = SessionInterop.GetSessionString(SessionInterop.WTS_INFO_CLASS.WTSUserName);
            }
            else if (e.Reason == SessionInterop.WTS_SESSION_LOGOFF ||
                     e.Reason == SessionInterop.WTS_SESSION_DISCONNECT)
            {
                _currentUserId = null;
            }

            // Only raise event if state actually changed
            if (newState != oldState)
            {
                _currentState = newState;

                var reasonName = e.Reason switch
                {
                    SessionInterop.WTS_SESSION_LOGON => "Logon",
                    SessionInterop.WTS_SESSION_LOGOFF => "Logoff",
                    SessionInterop.WTS_SESSION_LOCK => "Lock",
                    SessionInterop.WTS_SESSION_UNLOCK => "Unlock",
                    SessionInterop.WTS_CONSOLE_CONNECT => "ConsoleConnect",
                    SessionInterop.WTS_CONSOLE_DISCONNECT => "ConsoleDisconnect",
                    _ => $"Unknown({e.Reason})"
                };

                _logger.LogInformation(
                    "Session state changed: {OldState} → {NewState} (Reason: {Reason}, SessionId: {SessionId}, UserId: {UserId})",
                    oldState,
                    newState,
                    reasonName,
                    e.SessionId,
                    _currentUserId ?? "Unknown");

                SessionStateChanged?.Invoke(this, new SessionStateChangeEventArgs
                {
                    OldState = oldState,
                    NewState = newState,
                    Timestamp = DateTime.UtcNow,
                    UserId = _currentUserId
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling session state change event");
        }
    }
}
