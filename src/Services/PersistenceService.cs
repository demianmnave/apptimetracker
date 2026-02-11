using AppTimeTracker.Data;
using AppTimeTracker.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AppTimeTracker.Services;

/// <summary>
/// Configuration settings for telemetry persistence.
/// </summary>
public class TelemetrySettings
{
    public int EventBufferSize { get; set; } = 1000;
    public int FlushIntervalMs { get; set; } = 5000;
    public int RetryCount { get; set; } = 3;
    public int RetryDelayMs { get; set; } = 100;
}

/// <summary>
/// Service to flush buffered events to SQLite via EF Core in batches.
/// Implements deduplication and idempotency logic.
/// </summary>
public class PersistenceService : BackgroundService
{
    private readonly ILogger<PersistenceService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ITelemetryService _telemetryService;
    private readonly TelemetrySettings _settings;
    private readonly HashSet<string> _seenCorrelationIds = new();

    public PersistenceService(
        ILogger<PersistenceService> logger,
        IServiceProvider serviceProvider,
        ITelemetryService telemetryService,
        IOptions<TelemetrySettings> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
        _settings = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PersistenceService started with {FlushInterval}ms flush interval", _settings.FlushIntervalMs);

        var flushInterval = TimeSpan.FromMilliseconds(_settings.FlushIntervalMs);

        try
        {
            // Wait for initial startup
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await FlushBufferedEventsAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during persistence flush cycle");
                }

                await Task.Delay(flushInterval, stoppingToken);
            }

            // Final flush on shutdown
            await FlushBufferedEventsAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("PersistenceService shutdown requested");
        }
        finally
        {
            _logger.LogInformation("PersistenceService stopped");
        }
    }

    /// <summary>
    /// Flushes buffered events to the database in batches with deduplication.
    /// </summary>
    private async Task FlushBufferedEventsAsync(CancellationToken cancellationToken)
    {
        var bufferedEvents = _telemetryService.GetBufferedEvents();

        if (bufferedEvents.Count == 0)
        {
            return;
        }

        _logger.LogDebug("Flushing {EventCount} buffered events to database", bufferedEvents.Count);

        // Filter out duplicates based on CorrelationId
        var eventsToFlush = new List<FocusEvent>();
        foreach (var evt in bufferedEvents)
        {
            if (string.IsNullOrEmpty(evt.CorrelationId))
            {
                // No correlation ID, allow it
                eventsToFlush.Add(evt);
            }
            else if (!_seenCorrelationIds.Contains(evt.CorrelationId))
            {
                // New correlation ID, allow it and track it
                eventsToFlush.Add(evt);
                _seenCorrelationIds.Add(evt.CorrelationId);
            }
            else
            {
                // Duplicate detected, skip it
                _logger.LogDebug("Duplicate event detected: CorrelationId={CorrelationId}", evt.CorrelationId);
            }
        }

        if (eventsToFlush.Count == 0)
        {
            return;
        }

        // Attempt to persist with retry logic
        int attempt = 0;
        bool success = false;

        while (attempt < _settings.RetryCount && !success)
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    // Set persisted timestamp
                    var now = DateTime.UtcNow;
                    foreach (var evt in eventsToFlush)
                    {
                        evt.PersistedAtUtc = now;
                    }

                    // Add events to context
                    await dbContext.FocusEvents.AddRangeAsync(eventsToFlush, cancellationToken);

                    // Save with timeout
                    var saveTask = dbContext.SaveChangesAsync(cancellationToken);
                    var completedTask = await Task.WhenAny(
                        saveTask,
                        Task.Delay(TimeSpan.FromSeconds(10), cancellationToken));

                    if (completedTask != saveTask)
                    {
                        throw new TimeoutException("Database save operation timed out");
                    }

                    var saveResult = await saveTask;

                    _logger.LogInformation("Successfully persisted {EventCount} events to database (attempt {Attempt})",
                        saveResult, attempt + 1);

                    // Remove persisted events from telemetry buffer
                    var persistedIds = eventsToFlush.Select(e => e.Id).ToList();
                    _telemetryService.ClearPersistedEvents(persistedIds);

                    success = true;
                }
            }
            catch (Exception ex)
            {
                attempt++;
                _logger.LogWarning(ex, "Failed to persist events (attempt {Attempt}/{RetryCount})",
                    attempt, _settings.RetryCount);

                if (attempt < _settings.RetryCount)
                {
                    // Exponential backoff
                    var delayMs = _settings.RetryDelayMs * (int)Math.Pow(2, attempt - 1);
                    await Task.Delay(TimeSpan.FromMilliseconds(delayMs), cancellationToken);
                }
            }
        }

        if (!success)
        {
            _logger.LogError("Failed to persist events after {RetryCount} attempts. Events remain in buffer.",
                _settings.RetryCount);
        }
    }
}
