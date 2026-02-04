namespace AppTimeTracker.Services;

/// <summary>
/// Enumerates the possible user session states.
/// </summary>
public enum SessionState
{
    Active,        // User session is active and unlocked
    Locked,        // Workstation is locked but session still active
    Disconnected   // Session is disconnected (fast user switch, logout, etc.)
}

/// <summary>
/// Event arguments for session state changes.
/// </summary>
public class SessionStateChangeEventArgs : EventArgs
{
    public SessionState OldState { get; set; }
    public SessionState NewState { get; set; }
    public DateTime Timestamp { get; set; }
    public string? UserId { get; set; }
}

/// <summary>
/// Service that monitors Windows session state changes (lock/unlock/disconnect/reconnect).
/// Provides session state information and events for pause/resume of tracking.
/// </summary>
public interface ISessionMonitorService : IHostedService
{
    /// <summary>
    /// Fired when session state changes (lock/unlock/disconnect/reconnect).
    /// </summary>
    event EventHandler<SessionStateChangeEventArgs>? SessionStateChanged;

    /// <summary>
    /// Gets the ID of the currently signed-in user.
    /// </summary>
    string? CurrentUserId { get; }

    /// <summary>
    /// Gets the current session state (Active, Locked, Disconnected).
    /// </summary>
    SessionState CurrentState { get; }

    /// <summary>
    /// Gets whether the user session is currently active and not locked.
    /// </summary>
    bool IsSessionActive { get; }
}
