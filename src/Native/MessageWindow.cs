using System.Runtime.InteropServices;

namespace AppTimeTracker.Native;

/// <summary>
/// Hidden message-only window used to receive Terminal Services session change notifications.
/// Runs on a dedicated STA thread and uses WTSRegisterSessionNotification to subscribe to session events.
/// </summary>
public class MessageWindow : IDisposable
{
    private readonly ILogger<MessageWindow> _logger;
    private IntPtr _hwnd = IntPtr.Zero;
    private Thread? _messageThread;
    private ManualResetEvent? _windowCreatedEvent;
    private bool _disposed;

    // Window procedure delegate - must be kept alive to prevent GC
    private WndProcDelegate? _wndProcDelegate;

    // Window procedure delegate type definition
    public delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    public event EventHandler<SessionStateEventArgs>? SessionStateChanged;

    public MessageWindow(ILogger<MessageWindow> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Creates the message window on a dedicated STA thread.
    /// </summary>
    public void CreateWindow()
    {
        if (_hwnd != IntPtr.Zero)
        {
            _logger.LogWarning("Message window already created");
            return;
        }

        _windowCreatedEvent = new ManualResetEvent(false);
        _messageThread = new Thread(CreateMessageWindow)
        {
            IsBackground = true,
            Name = "SessionMonitorMessageWindow"
        };
        _messageThread.SetApartmentState(ApartmentState.STA);
        _messageThread.Start();

        // Wait for window to be created before returning
        if (!_windowCreatedEvent.WaitOne(TimeSpan.FromSeconds(5)))
        {
            _logger.LogError("Failed to create message window within timeout");
            throw new TimeoutException("Message window creation timed out");
        }

        _logger.LogInformation("Message window created successfully (HWND: 0x{Handle:X})", _hwnd);
    }

    /// <summary>
    /// Destroys the message window and stops the message thread.
    /// </summary>
    public void DestroyWindow()
    {
        if (_hwnd != IntPtr.Zero)
        {
            SessionInterop.DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
            _logger.LogInformation("Message window destroyed");
        }

        _messageThread?.Join(TimeSpan.FromSeconds(5));
    }

    private void CreateMessageWindow()
    {
        try
        {
            // Register window class
            var hInstance = Marshal.GetHINSTANCE(typeof(MessageWindow).Module);

            // Create the window procedure delegate
            _wndProcDelegate = WindowProc;

            var wndClass = new SessionInterop.WNDCLASS
            {
                lpszClassName = SessionInterop.MESSAGE_WINDOW_CLASS,
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
                hInstance = hInstance
            };

            if (SessionInterop.RegisterClass(ref wndClass) == 0)
            {
                var error = Marshal.GetLastWin32Error();
                if (error != 1410) // Class already exists - OK for restart scenarios
                {
                    _logger.LogError("Failed to register window class: error {Error}", error);
                    _windowCreatedEvent?.Set();
                    return;
                }
            }

            // Create message-only window
            _hwnd = SessionInterop.CreateWindowEx(
                0,
                SessionInterop.MESSAGE_WINDOW_CLASS,
                string.Empty,
                0,
                0, 0, 0, 0,
                new IntPtr(SessionInterop.HWND_MESSAGE),
                IntPtr.Zero,
                hInstance,
                IntPtr.Zero);

            if (_hwnd == IntPtr.Zero)
            {
                _logger.LogError("Failed to create message window: error {Error}", Marshal.GetLastWin32Error());
                _windowCreatedEvent?.Set();
                return;
            }

            // Register for session notifications
            if (!SessionInterop.WTSRegisterSessionNotification(_hwnd, 0))
            {
                _logger.LogError("Failed to register for session notifications: error {Error}", Marshal.GetLastWin32Error());
                SessionInterop.DestroyWindow(_hwnd);
                _hwnd = IntPtr.Zero;
                _windowCreatedEvent?.Set();
                return;
            }

            _logger.LogInformation("Registered for session notifications");
            _windowCreatedEvent?.Set();

            // Message loop
            var msg = new MSG();
            while (GetMessage(ref msg, IntPtr.Zero, 0, 0) > 0)
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }

            _logger.LogInformation("Message window thread exiting");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in message window creation");
            _windowCreatedEvent?.Set();
        }
        finally
        {
            if (_hwnd != IntPtr.Zero)
            {
                SessionInterop.WTSUnRegisterSessionNotification(_hwnd);
            }
        }
    }

    private IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == SessionInterop.WM_WTSSESSION_CHANGE)
        {
            var reason = (uint)wParam.ToInt32();
            var sessionId = (uint)lParam.ToInt32();

            var stateChange = reason switch
            {
                SessionInterop.WTS_SESSION_LOGON => "Logon",
                SessionInterop.WTS_SESSION_LOGOFF => "Logoff",
                SessionInterop.WTS_SESSION_LOCK => "Lock",
                SessionInterop.WTS_SESSION_UNLOCK => "Unlock",
                SessionInterop.WTS_CONSOLE_CONNECT => "ConsoleConnect",
                SessionInterop.WTS_CONSOLE_DISCONNECT => "ConsoleDisconnect",
                _ => $"Unknown({reason})"
            };

            _logger.LogInformation("Session change: {StateChange} (SessionId: {SessionId})", stateChange, sessionId);

            var args = new SessionStateEventArgs
            {
                Reason = reason,
                SessionId = sessionId
            };

            SessionStateChanged?.Invoke(this, args);
            return IntPtr.Zero;
        }

        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [DllImport("user32.dll")]
    private static extern int GetMessage(ref MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    public void Dispose()
    {
        if (_disposed)
            return;

        DestroyWindow();
        _windowCreatedEvent?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Event arguments for session state changes.
/// </summary>
public class SessionStateEventArgs : EventArgs
{
    public uint Reason { get; set; }
    public uint SessionId { get; set; }
}
