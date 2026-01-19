namespace AppTimeTracker.Services;

/// <summary>
/// Event arguments for focus change events.
/// </summary>
public class FocusChangeEventArgs : EventArgs
{
    /// <summary>
    /// The process name of the focused application.
    /// </summary>
    public required string ProcessName { get; set; }

    /// <summary>
    /// The full path to the executable file.
    /// </summary>
    public string? ExecutablePath { get; set; }

    /// <summary>
    /// The title of the focused window.
    /// </summary>
    public string? WindowTitle { get; set; }

    /// <summary>
    /// The process ID of the focused application.
    /// </summary>
    public uint ProcessId { get; set; }

    /// <summary>
    /// Timestamp when the focus change occurred.
    /// </summary>
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Service that monitors Windows foreground window changes.
/// Provides events for application focus tracking.
/// </summary>
public interface IFocusMonitorService : IHostedService
{
    /// <summary>
    /// Fired when the foreground window changes (application gains focus).
    /// </summary>
    event EventHandler<FocusChangeEventArgs>? FocusChanged;

    /// <summary>
    /// Gets a value indicating whether the WinEventHook is currently active.
    /// </summary>
    bool IsHookActive { get; }
}
