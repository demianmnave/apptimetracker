using System.Diagnostics;
using Serilog;
using Serilog.Context;
using AppTimeTracker.Data;
using AppTimeTracker.Models;
using System.Text.Json;
using System.Collections.Concurrent;

namespace AppTimeTracker.Services;

/// <summary>
/// Interface for structured logging with context enrichment and database persistence.
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

    /// <summary>
    /// Persists a log entry to the database asynchronously (non-blocking).
    /// </summary>
    Task PersistLogAsync(LogSeverity severity, string message, string sourceComponent, string userId, 
        string? contextData = null, string? correlationId = null, string? stackTrace = null, 
        string? additionalMetadata = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of structured logging with context enrichment, correlation tracking, and database persistence.
/// </summary>
public class LoggingService : ILoggingService
{
    private readonly ILogger<LoggingService> _logger;
    private readonly IAppLogRepository? _logRepository;
    private readonly IHostApplicationLifetime? _hostLifetime;
    private static readonly object _correlationLock = new();
    private static readonly Dictionary<int, string> _threadCorrelations = new();
    private readonly ConcurrentQueue<Func<CancellationToken, Task>> _logPersistenceQueue;
    private CancellationTokenSource? _persistenceCts;

    public LoggingService(ILogger<LoggingService> logger, IAppLogRepository? logRepository = null, 
        IHostApplicationLifetime? hostLifetime = null)
    {
        _logger = logger;
        _logRepository = logRepository;
        _hostLifetime = hostLifetime;
        _logPersistenceQueue = new ConcurrentQueue<Func<CancellationToken, Task>>();
    }

    /// <summary>
    /// Initializes the logging service and starts background persistence task.
    /// </summary>
    public void Initialize()
    {
        if (_logRepository == null)
        {
            _logger.LogWarning("AppLogRepository is not configured. Database persistence will be disabled.");
            return;
        }

        _persistenceCts = new CancellationTokenSource();

        // Register for graceful shutdown
        if (_hostLifetime != null)
        {
            _hostLifetime.ApplicationStopping.Register(() =>
            {
                _persistenceCts?.Cancel();
            });
        }

        // Start background task for processing log persistence queue
        _ = ProcessLogPersistenceQueueAsync(_persistenceCts.Token);
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

        // Persist error to database asynchronously
        if (_logRepository != null)
        {
            var contextJson = TrySerializeToJson(additionalData);
            var logTask = PersistLogAsync(
                ConvertLogLevelToSeverity(level),
                message,
                "LoggingService",
                Environment.UserName,
                contextJson,
                correlationId,
                exception.StackTrace
            );
            // Fire and forget, don't await
            _ = logTask;
        }
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
    /// Persists a log entry to the database asynchronously (non-blocking via queue).
    /// </summary>
    public async Task PersistLogAsync(LogSeverity severity, string message, string sourceComponent, string userId,
        string? contextData = null, string? correlationId = null, string? stackTrace = null,
        string? additionalMetadata = null, CancellationToken cancellationToken = default)
    {
        if (_logRepository == null)
        {
            return;
        }

        // Queue the persistence operation instead of awaiting it
        _logPersistenceQueue.Enqueue(async (ct) =>
        {
            try
            {
                var appLog = new AppLog
                {
                    Severity = severity,
                    Message = message,
                    SourceComponent = sourceComponent,
                    UserId = userId,
                    ContextData = contextData,
                    CorrelationId = correlationId,
                    StackTrace = stackTrace,
                    AdditionalMetadata = additionalMetadata,
                    Timestamp = DateTime.UtcNow
                };

                var (isValid, errorMessage) = _logRepository.ValidateLog(appLog);
                if (!isValid)
                {
                    _logger.LogWarning("Log validation failed: {Error}. Message: {Message}", errorMessage, message);
                    return;
                }

                await _logRepository.CreateLogAsync(appLog, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist log entry to database");
            }
        });

        // Allow one tick of async work to complete the current operation
        await Task.Yield();
    }

    /// <summary>
    /// Processes the log persistence queue in the background.
    /// </summary>
    private async Task ProcessLogPersistenceQueueAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                int processed = 0;

                // Process up to 10 items per iteration to avoid blocking
                while (processed < 10 && _logPersistenceQueue.TryDequeue(out var persistTask))
                {
                    try
                    {
                        await persistTask(cancellationToken);
                        processed++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing log persistence queue item");
                    }
                }

                // Small delay to prevent busy-waiting
                if (processed == 0)
                {
                    await Task.Delay(100, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Graceful shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in log persistence queue processor");
                await Task.Delay(100, cancellationToken);
            }
        }

        // Flush any remaining items on shutdown
        while (_logPersistenceQueue.TryDequeue(out var persistTask))
        {
            try
            {
                await persistTask(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error flushing remaining log persistence queue item during shutdown");
            }
        }
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
    /// Converts LogLevel to LogSeverity enum.
    /// </summary>
    private static LogSeverity ConvertLogLevelToSeverity(LogLevel logLevel)
    {
        return logLevel switch
        {
            LogLevel.Trace => LogSeverity.Debug,
            LogLevel.Debug => LogSeverity.Debug,
            LogLevel.Information => LogSeverity.Info,
            LogLevel.Warning => LogSeverity.Warning,
            LogLevel.Error => LogSeverity.Error,
            LogLevel.Critical => LogSeverity.Critical,
            LogLevel.None => LogSeverity.Info,
            _ => LogSeverity.Info
        };
    }

    /// <summary>
    /// Attempts to serialize an object to JSON string.
    /// </summary>
    private static string? TrySerializeToJson(object? obj)
    {
        if (obj == null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Serialize(obj);
        }
        catch
        {
            return obj.ToString();
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
