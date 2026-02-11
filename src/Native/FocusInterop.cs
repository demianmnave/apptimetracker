using System.Runtime.InteropServices;

namespace AppTimeTracker.Native;

/// <summary>
/// P/Invoke declarations for Windows focus event monitoring (user32.dll).
/// Used to track foreground window changes and monitor application focus.
/// </summary>
public static class FocusInterop
{
    // Constants for SetWinEventHook
    public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;

    // Hook constants
    public const uint EVENT_MIN = 0;
    public const uint EVENT_MAX = unchecked((uint)-1);

    /// <summary>
    /// Delegate for the WinEventHook callback function.
    /// Called when a window event (such as foreground window change) occurs.
    /// </summary>
    /// <param name="hWinEventHook">Handle to the event hook</param>
    /// <param name="eventType">The event that occurred (e.g., EVENT_SYSTEM_FOREGROUND)</param>
    /// <param name="hwnd">Handle to the window that generated the event</param>
    /// <param name="idObject">The object ID (typically OBJID_WINDOW)</param>
    /// <param name="idChild">The child element ID</param>
    /// <param name="dwEventThread">The thread ID of the event</param>
    /// <param name="dwmsEventTime">The time of the event in milliseconds</param>
    public delegate void WinEventDelegate(
        IntPtr hWinEventHook,
        uint eventType,
        IntPtr hwnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime);

    /// <summary>
    /// Installs a hook procedure that monitors window events.
    /// Used to detect foreground window changes for focus tracking.
    /// </summary>
    /// <param name="eventMin">The lowest event value to monitor</param>
    /// <param name="eventMax">The highest event value to monitor</param>
    /// <param name="hmodWinEventHook">Module handle (IntPtr.Zero for all modules)</param>
    /// <param name="lpfnWinEventProc">Pointer to the callback function</param>
    /// <param name="idProcess">Process ID to monitor (0 for all processes)</param>
    /// <param name="idThread">Thread ID to monitor (0 for all threads)</param>
    /// <param name="dwFlags">Hook flags (WINEVENT_OUTOFCONTEXT)</param>
    /// <returns>Handle to the hook, or IntPtr.Zero on failure</returns>
    [DllImport("user32.dll")]
    public static extern IntPtr SetWinEventHook(
        uint eventMin,
        uint eventMax,
        IntPtr hmodWinEventHook,
        WinEventDelegate lpfnWinEventProc,
        uint idProcess,
        uint idThread,
        uint dwFlags);

    /// <summary>
    /// Removes an event hook installed by SetWinEventHook.
    /// </summary>
    /// <param name="hWinEventHook">Handle to the hook to remove</param>
    /// <returns>True if successful, false otherwise</returns>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    /// <summary>
    /// Retrieves a handle to the foreground window (the window with input focus).
    /// </summary>
    /// <returns>Handle to the foreground window, or IntPtr.Zero if no window</returns>
    [DllImport("user32.dll", SetLastError = false)]
    public static extern IntPtr GetForegroundWindow();

    /// <summary>
    /// Retrieves the process ID and thread ID of the window.
    /// </summary>
    /// <param name="hWnd">Handle to the window</param>
    /// <param name="lpdwProcessId">Output parameter for the process ID</param>
    /// <returns>The thread ID of the window</returns>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    /// <summary>
    /// Copies the text of a window's title bar to a buffer.
    /// </summary>
    /// <param name="hWnd">Handle to the window</param>
    /// <param name="lpString">Output buffer for the window title</param>
    /// <param name="nMaxCount">Maximum number of characters to copy</param>
    /// <returns>The length of the window title text</returns>
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    /// <summary>
    /// Flag for SetWinEventHook indicating the callback should not be in-context.
    /// This allows the callback to work even if the target process doesn't respond.
    /// </summary>
    public const uint WINEVENT_OUTOFCONTEXT = 0;
}
