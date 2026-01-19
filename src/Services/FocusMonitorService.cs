using System.Diagnostics;
using System.Text;
using AppTimeTracker.Native;

namespace AppTimeTracker.Services;

/// <summary>
/// Service that monitors Windows foreground window changes using SetWinEventHook.
/// Tracks which application has focus and fires events when focus changes.
/// </summary>
public class FocusMonitorService : IFocusMonitorService
{
    private readonly ILogger<FocusMonitorService> _logger;
    private IntPtr _focusEventHook = IntPtr.Zero;
    private FocusInterop.WinEventDelegate? _focusDelegate;
    private IntPtr _lastFocusedWindow = IntPtr.Zero;

    public event EventHandler<FocusChangeEventArgs>? FocusChanged;

    /// <summary>
    /// Gets a value indicating whether the WinEventHook is currently active.
    /// </summary>
    public bool IsHookActive => _focusEventHook != IntPtr.Zero;

    public FocusMonitorService(ILogger<FocusMonitorService> logger)
    {
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("FocusMonitorService starting...");

        try
        {
            // Create the event hook callback delegate
            _focusDelegate = new FocusInterop.WinEventDelegate(WinEventProc);

            // Install the event hook for EVENT_SYSTEM_FOREGROUND
            _focusEventHook = FocusInterop.SetWinEventHook(
                FocusInterop.EVENT_SYSTEM_FOREGROUND,
                FocusInterop.EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero,
                _focusDelegate,
                0,
                0,
                FocusInterop.WINEVENT_OUTOFCONTEXT);

            if (_focusEventHook == IntPtr.Zero)
            {
                _logger.LogError("Failed to install WinEventHook for foreground window tracking");
                return Task.CompletedTask;
            }

            _logger.LogInformation("Successfully installed WinEventHook (Handle: {HookHandle})", _focusEventHook);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting FocusMonitorService");
            return Task.CompletedTask;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("FocusMonitorService stopping...");

        try
        {
            if (_focusEventHook != IntPtr.Zero)
            {
                bool result = FocusInterop.UnhookWinEvent(_focusEventHook);
                if (result)
                {
                    _logger.LogInformation("Successfully unhooked WinEventHook");
                }
                else
                {
                    _logger.LogWarning("Failed to unhook WinEventHook");
                }

                _focusEventHook = IntPtr.Zero;
            }

            // Clear the delegate reference
            _focusDelegate = null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping FocusMonitorService");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Callback function invoked when a window event occurs (foreground window change).
    /// </summary>
    private void WinEventProc(
        IntPtr hWinEventHook,
        uint eventType,
        IntPtr hwnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime)
    {
        try
        {
            // Ignore if same window
            if (hwnd == _lastFocusedWindow)
            {
                return;
            }

            _lastFocusedWindow = hwnd;

            // Get the process ID from the window handle
            uint processId = 0;
            FocusInterop.GetWindowThreadProcessId(hwnd, out processId);

            if (processId == 0)
            {
                _logger.LogDebug("Foreground window has no process ID");
                return;
            }

            // Get the window title
            var titleBuilder = new StringBuilder(256);
            FocusInterop.GetWindowText(hwnd, titleBuilder, titleBuilder.Capacity);
            string windowTitle = titleBuilder.ToString();

            // Get process information
            string processName = string.Empty;
            string? executablePath = null;

            try
            {
                var process = Process.GetProcessById((int)processId);
                processName = process.ProcessName;
                executablePath = process.MainModule?.FileName;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to get process information for PID {ProcessId}", processId);
                processName = $"Process_{processId}";
            }

            // Fire the FocusChanged event
            var args = new FocusChangeEventArgs
            {
                ProcessName = processName,
                ExecutablePath = executablePath,
                WindowTitle = windowTitle,
                ProcessId = processId,
                Timestamp = DateTime.UtcNow
            };

            _logger.LogDebug(
                "Focus changed to: {ProcessName} (PID: {ProcessId}), Window: {WindowTitle}",
                processName,
                processId,
                windowTitle);

            FocusChanged?.Invoke(this, args);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in WinEventProc callback");
        }
    }
}
