using System.Collections.Concurrent;
using AppTimeTracker.Models;

namespace AppTimeTracker.Services;

/// <summary>
/// Interface for telemetry service that collects and buffers focus/session events.
/// </summary>
public interface ITelemetryService
{
    /// <summary>
    /// Records a focus or session event for later persistence.
    /// </summary>
    Task RecordEventAsync(FocusEvent focusEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all buffered events without removing them.
    /// </summary>
    IReadOnlyList<FocusEvent> GetBufferedEvents();

    /// <summary>
    /// Removes persisted events from the buffer.
    /// </summary>
    void ClearPersistedEvents(IEnumerable<int> eventIds);

    /// <summary>
    /// Gets the count of events currently in the buffer.
    /// </summary>
    int BufferedEventCount { get; }
}

/// <summary>
/// Service to collect, validate, and buffer focus/session events before persistence.
/// Implements event serialization and timestamping.
/// </summary>
public class TelemetryService : ITelemetryService
{
    private readonly ILogger<TelemetryService> _logger;
    private readonly ConcurrentQueue<FocusEvent> _eventBuffer;
    private readonly int _maxBufferSize;

    public TelemetryService(ILogger<TelemetryService> logger, IConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        var telemetrySettings = configuration.GetSection("TelemetrySettings");
        _maxBufferSize = telemetrySettings.GetValue("EventBufferSize", 1000);
        
        _eventBuffer = new ConcurrentQueue<FocusEvent>();

        _logger.LogInformation("TelemetryService initialized with max buffer size: {BufferSize}", _maxBufferSize);
    }

    /// <summary>
    /// Records a focus or session event for later persistence.
    /// Validates the event and adds it to the buffer.
    /// </summary>
    public async Task RecordEventAsync(FocusEvent focusEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate event
            if (focusEvent == null)
            {
                _logger.LogWarning("Attempted to record null focus event");
                return;
            }

            if (string.IsNullOrWhiteSpace(focusEvent.ProcessName))
            {
                _logger.LogWarning("Attempted to record focus event with empty ProcessName");
                return;
            }

            if (string.IsNullOrWhiteSpace(focusEvent.UserId))
            {
                _logger.LogWarning("Attempted to record focus event with empty UserId");
                return;
            }

            // Ensure timestamps are set
            if (focusEvent.StartTimeUtc == default)
            {
                focusEvent.StartTimeUtc = DateTime.UtcNow;
            }

            if (focusEvent.EventDate == default)
            {
                focusEvent.EventDate = DateOnly.FromDateTime(focusEvent.StartTimeUtc);
            }

            // Check buffer size
            if (_eventBuffer.Count >= _maxBufferSize)
            {
                _logger.LogWarning("Telemetry buffer at capacity ({BufferSize}). Dropping oldest event.", _maxBufferSize);
                _eventBuffer.TryDequeue(out _);
            }

            // Add to buffer
            _eventBuffer.Enqueue(focusEvent);

            _logger.LogDebug("Recorded focus event: Process={ProcessName}, Type={EventType}, User={UserId}, BufferSize={BufferSize}",
                focusEvent.ProcessName, focusEvent.EventType, focusEvent.UserId, _eventBuffer.Count);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording focus event");
        }
    }

    /// <summary>
    /// Gets all buffered events without removing them.
    /// </summary>
    public IReadOnlyList<FocusEvent> GetBufferedEvents()
    {
        return _eventBuffer.ToList().AsReadOnly();
    }

    /// <summary>
    /// Removes persisted events from the buffer by their IDs.
    /// </summary>
    public void ClearPersistedEvents(IEnumerable<int> eventIds)
    {
        var idsToRemove = new HashSet<int>(eventIds);
        var remaining = new List<FocusEvent>();

        while (_eventBuffer.TryDequeue(out var evt))
        {
            if (!idsToRemove.Contains(evt.Id))
            {
                remaining.Add(evt);
            }
        }

        foreach (var evt in remaining)
        {
            _eventBuffer.Enqueue(evt);
        }

        _logger.LogDebug("Cleared {RemovedCount} persisted events from buffer. Remaining: {RemainingCount}",
            idsToRemove.Count, _eventBuffer.Count);
    }

    /// <summary>
    /// Gets the count of events currently in the buffer.
    /// </summary>
    public int BufferedEventCount => _eventBuffer.Count;
}
