using System.Diagnostics;
using Serilog;
using Serilog.Context;

namespace AppTimeTracker.Services;

/// <summary>
/// Interface for structured logging with context enrichment.
/// </summary>
public interface ILoggingService
{
    /// <summary>
    /// Logs a message with structured context including correlation ID and process info.
    /// </summary>
    void LogWithContext(LogLevel level, string message, string? correlationId = null, Dictionary<string, object>? additionalData = null);

    /// <summary>
    /// Logs an exception with full context and stack trace.
    /// </summary>
    void LogException(Exception exception, string message, LogLevel level = LogLevel.Error, string? correlationId = null);

    /// <summary>
    /// Creates a logging scope with correlation ID and thread/process context.
    /// </summary>
    IDisposable CreateScope(string correlationId);

    /// <summary>
    /// Generates a new correlation ID for tracing related events.
    /// </summary>
    string GenerateCorrelationId();
}

/// <summary>
/// Implementation of structured logging with context enrichment and correlation tracking.
/// </summary>
public class LoggingService : ILoggingService
{
    private readonly ILogger<LoggingService> _logger;
    private static readonly object _correlationLock = new();
    private static readonly Dictionary<int, string> _threadCorrelations = new();

    public LoggingService(ILogger<LoggingService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Logs a message with structured context including correlation ID and process info.
    /// </summary>
    public void LogWithContext(LogLevel level, string message, string? correlationId = null, Dictionary<string, object>? additionalData = null)
    {
        correlationId ??= GetOrCreateCorrelationId();

        var threadId = Thread.CurrentThread.ManagedThreadId;
        var processId = Process.GetCurrentProcess().Id;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("ThreadId", threadId))
        using (LogContext.PushProperty("ProcessId", processId))
        {
            if (additionalData != null)
            {
                foreach (var kvp in additionalData)
                {
                    LogContext.PushProperty(kvp.Key, kvp.Value);
                }
            }

            _logger.Log(level, message);
        }
    }

    /// <summary>
    /// Logs an exception with full context and stack trace.
    /// </summary>
    public void LogException(Exception exception, string message, LogLevel level = LogLevel.Error, string? correlationId = null)
    {
        correlationId ??= GetOrCreateCorrelationId();

        var threadId = Thread.CurrentThread.ManagedThreadId;
        var processId = Process.GetCurrentProcess().Id;

        var additionalData = new Dictionary<string, object>
        {
            { "ExceptionType", exception.GetType().FullName ?? "Unknown" },
            { "ExceptionMessage", exception.Message },
            { "StackTrace", exception.StackTrace ?? "No stack trace" },
            { "ThreadId", threadId },
            { "ProcessId", processId },
            { "CorrelationId", correlationId }
        };

        LogWithContext(level, "{Message}: {Exception}", correlationId, additionalData);
        _logger.Log(level, exception, message);
    }

    /// <summary>
    /// Creates a logging scope with correlation ID and thread/process context.
    /// </summary>
    public IDisposable CreateScope(string correlationId)
    {
        var threadId = Thread.CurrentThread.ManagedThreadId;
        var processId = Process.GetCurrentProcess().Id;

        var scope = new LoggingScope(correlationId, threadId, processId);
        StoreCorrelationId(correlationId);
        return scope;
    }

    /// <summary>
    /// Generates a new correlation ID for tracing related events.
    /// </summary>
    public string GenerateCorrelationId()
    {
        return $"{Process.GetCurrentProcess().Id}-{Thread.CurrentThread.ManagedThreadId}-{Guid.NewGuid():N}";
    }

    /// <summary>
    /// Gets the current correlation ID or creates a new one if none exists.
    /// </summary>
    private string GetOrCreateCorrelationId()
    {
        var threadId = Thread.CurrentThread.ManagedThreadId;

        lock (_correlationLock)
        {
            if (_threadCorrelations.TryGetValue(threadId, out var correlationId))
            {
                return correlationId;
            }

            var newCorrelationId = GenerateCorrelationId();
            _threadCorrelations[threadId] = newCorrelationId;
            return newCorrelationId;
        }
    }

    /// <summary>
    /// Stores a correlation ID for the current thread.
    /// </summary>
    private void StoreCorrelationId(string correlationId)
    {
        var threadId = Thread.CurrentThread.ManagedThreadId;

        lock (_correlationLock)
        {
            _threadCorrelations[threadId] = correlationId;
        }
    }

    /// <summary>
    /// Helper class for implementing IDisposable logging scope.
    /// </summary>
    private class LoggingScope : IDisposable
    {
        private readonly IDisposable?[] _disposables;

        public LoggingScope(string correlationId, int threadId, int processId)
        {
            _disposables = new IDisposable?[]
            {
                LogContext.PushProperty("CorrelationId", correlationId),
                LogContext.PushProperty("ThreadId", threadId),
                LogContext.PushProperty("ProcessId", processId)
            };
        }

        public void Dispose()
        {
            foreach (var disposable in _disposables)
            {
                disposable?.Dispose();
            }
        }
    }
}

/// <summary>
/// Serilog context helper for pushing properties into the logging context.
/// </summary>
public static class LogContext
{
    /// <summary>
    /// Pushes a property into the current logging context.
    /// </summary>
    public static IDisposable PushProperty(string name, object? value)
    {
        return Serilog.Context.LogContext.PushProperty(name, value);
    }
}
