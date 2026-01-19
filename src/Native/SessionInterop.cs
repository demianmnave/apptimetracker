using System.Runtime.InteropServices;

namespace AppTimeTracker.Native;

/// <summary>
/// P/Invoke declarations for Windows Session APIs (wtsapi32.dll and user32.dll).
/// Used to query and monitor user session state including lock/unlock events.
/// </summary>
public static class SessionInterop
{
    // Constants for session state messages
    public const uint WM_WTSSESSION_CHANGE = 0x2B1;
    public const uint WTS_SESSION_LOGON = 5;
    public const uint WTS_SESSION_LOGOFF = 6;
    public const uint WTS_SESSION_LOCK = 7;
    public const uint WTS_SESSION_UNLOCK = 8;
    public const uint WTS_CONSOLE_CONNECT = 1;
    public const uint WTS_CONSOLE_DISCONNECT = 2;

    // Constants for window creation
    public const uint WS_OVERLAPPEDWINDOW = 0x00CF0000;
    public const int HWND_MESSAGE = -3;

    // Window class name for message-only window
    public const string MESSAGE_WINDOW_CLASS = "AppTimeTrackerSessionWindow";

    /// <summary>
    /// Enumerates the session information types available from WTSQuerySessionInformation.
    /// </summary>
    public enum WTS_INFO_CLASS
    {
        WTSInitialProgram,
        WTSApplicationName,
        WTSWorkingDirectory,
        WTSOEMId,
        WTSSessionId,
        WTSUserName,
        WTSWinStationName,
        WTSDomainName,
        WTSConnectState,
        WTSClientBuildNumber,
        WTSClientName,
        WTSClientDirectory,
        WTSClientProductId,
        WTSClientHardwareId,
        WTSClientAddress,
        WTSClientAddressV6,
        WTSClientProtocolType,
        WTSIdleTime,
        WTSLogonTime,
        WTSIncomingBytes,
        WTSOutgoingBytes,
        WTSIncomingFrames,
        WTSOutgoingFrames,
        WTSClientSessionInfo,
        WTSSessionInfoEx,
        WTSConfigInfo,
        WTSValidationInfo,
        WTSSessionAddressV4,
        WTSIsRemoteSession
    }

    /// <summary>
    /// Represents session information retrieved via WTSQuerySessionInformation.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct WTS_SESSION_INFO
    {
        public uint SessionId;
        [MarshalAs(UnmanagedType.LPStr)]
        public string pWinStationName;
        public uint State;
    }

    /// <summary>
    /// Gets the session ID of the active console session (interactive user session).
    /// </summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint WTSGetActiveConsoleSessionId();

    /// <summary>
    /// Maps a process ID to its corresponding session ID.
    /// </summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ProcessIdToSessionId(uint dwProcessId, out uint pSessionId);

    /// <summary>
    /// Queries information about a specific Terminal Services session.
    /// </summary>
    [DllImport("wtsapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool WTSQuerySessionInformation(
        IntPtr hServer,
        uint SessionId,
        WTS_INFO_CLASS WTSInfoClass,
        out IntPtr ppBuffer,
        out uint pBytesReturned);

    /// <summary>
    /// Frees memory allocated by WTSQuerySessionInformation and other Terminal Services APIs.
    /// </summary>
    [DllImport("wtsapi32.dll", SetLastError = true)]
    public static extern void WTSFreeMemory(IntPtr pMemory);

    /// <summary>
    /// Registers a window to receive Terminal Services session change notifications.
    /// </summary>
    [DllImport("wtsapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool WTSRegisterSessionNotification(
        IntPtr hWnd,
        uint dwFlags);

    /// <summary>
    /// Unregisters a window from receiving Terminal Services session change notifications.
    /// </summary>
    [DllImport("wtsapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool WTSUnRegisterSessionNotification(IntPtr hWnd);

    /// <summary>
    /// Creates a window with the specified parameters.
    /// </summary>
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern IntPtr CreateWindowEx(
        uint dwExStyle,
        string lpClassName,
        string lpWindowName,
        uint dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    /// <summary>
    /// Destroys a window and frees associated resources.
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DestroyWindow(IntPtr hWnd);

    /// <summary>
    /// Registers a window class.
    /// </summary>
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern ushort RegisterClass(ref WNDCLASS lpWndClass);

    /// <summary>
    /// Unregisters a window class.
    /// </summary>
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnregisterClass(string lpClassName, IntPtr hInstance);

    /// <summary>
    /// Defines the window class structure.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct WNDCLASS
    {
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string lpszMenuName;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string lpszClassName;
    }

    /// <summary>
    /// Retrieves the string value from WTS session information buffer.
    /// </summary>
    public static string? GetSessionString(WTS_INFO_CLASS infoClass)
    {
        return GetSessionString(IntPtr.Zero, WTSGetActiveConsoleSessionId(), infoClass);
    }

    /// <summary>
    /// Retrieves the string value from WTS session information buffer for a specific session.
    /// </summary>
    public static string? GetSessionString(IntPtr hServer, uint sessionId, WTS_INFO_CLASS infoClass)
    {
        try
        {
            if (!WTSQuerySessionInformation(hServer, sessionId, infoClass, out var ppBuffer, out var pBytesReturned))
            {
                return null;
            }

            try
            {
                return Marshal.PtrToStringAnsi(ppBuffer);
            }
            finally
            {
                WTSFreeMemory(ppBuffer);
            }
        }
        catch
        {
            return null;
        }
    }
}
