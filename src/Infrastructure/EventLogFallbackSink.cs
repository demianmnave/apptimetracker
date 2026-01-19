using System.Diagnostics;
using System.Runtime.InteropServices;
using Serilog.Core;
using Serilog.Events;

namespace AppTimeTracker.Infrastructure;

/// <summary>
/// Custom Serilog sink that provides Windows Event Log fallback when file logging fails.
/// Attempts to write critical errors to Event Log if file sink encounters failures.
/// </summary>
public class EventLogFallbackSink : ILogEventSink
{
    private readonly ILogEventSink _innerSink;
    private readonly string _eventLogSource;
    private static readonly object _lockObject = new();

    public EventLogFallbackSink(ILogEventSink innerSink, string eventLogSource = "AppTimeTracker")
    {
        _innerSink = innerSink ?? throw new ArgumentNullException(nameof(innerSink));
        _eventLogSource = eventLogSource;
    }

    /// <summary>
    /// Emits a log event to the inner sink, with Event Log fallback on failure.
    /// </summary>
    public void Emit(LogEvent logEvent)
    {
        try
        {
            _innerSink.Emit(logEvent);
        }
        catch (Exception ex)
        {
            // If file logging fails, attempt to write to Event Log (Windows only)
            if (logEvent.Level >= LogEventLevel.Error && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                TryWriteToEventLog(logEvent, ex);
            }

            // Re-throw to ensure the error is not silently swallowed
            throw;
        }
    }

    /// <summary>
    /// Attempts to write a log event to the Windows Event Log as a fallback mechanism.
    /// </summary>
    private void TryWriteToEventLog(LogEvent logEvent, Exception sinkException)
    {
        try
        {
            lock (_lockObject)
            {
                if (!EventLog.SourceExists(_eventLogSource))
                {
                    EventLog.CreateEventSource(_eventLogSource, "Application");
                }

                using (var eventLog = new EventLog("Application"))
                {
                    eventLog.Source = _eventLogSource;

                    var eventType = logEvent.Level switch
                    {
                        LogEventLevel.Fatal => EventLogEntryType.Error,
                        LogEventLevel.Error => EventLogEntryType.Error,
                        LogEventLevel.Warning => EventLogEntryType.Warning,
                        _ => EventLogEntryType.Information
                    };

                    var message = $"File logging failed: {sinkException.Message}\n\n" +
                                 $"Original log level: {logEvent.Level}\n" +
                                 $"Original message: {logEvent.MessageTemplate}\n" +
                                 $"Timestamp: {logEvent.Timestamp:O}";

                    eventLog.WriteEntry(message, eventType, 1000);
                }
            }
        }
        catch
        {
            // Silently ignore Event Log failures to prevent cascading errors
        }
    }
}

/// <summary>
/// Extension methods for configuring EventLogFallback sink in Serilog.
/// </summary>
public static class EventLogFallbackSinkExtensions
{
    /// <summary>
    /// Configures Serilog to use EventLogFallback sink with the specified inner sink.
    /// </summary>
    public static LoggerConfiguration WithEventLogFallback(
        this LoggerSinkConfiguration sinkConfiguration,
        ILogEventSink innerSink,
        string eventLogSource = "AppTimeTracker")
    {
        return sinkConfiguration.Sink(new EventLogFallbackSink(innerSink, eventLogSource));
    }
}
