using AppTimeTracker.Data;
using AppTimeTracker.Models;

namespace AppTimeTracker.Services;

/// <summary>
/// DTO for daily usage statistics.
/// </summary>
public class DailyUsageReportDto
{
    /// <summary>
    /// Date of the report.
    /// </summary>
    public DateOnly ReportDate { get; set; }

    /// <summary>
    /// User ID for the report.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Total usage time in seconds for all applications on this date.
    /// </summary>
    public long TotalUsageSeconds { get; set; }

    /// <summary>
    /// Total number of sessions tracked on this date.
    /// </summary>
    public int TotalSessions { get; set; }

    /// <summary>
    /// Breakdown of usage by process name (process name -> duration in seconds).
    /// </summary>
    public Dictionary<string, long> UsageByProcess { get; set; } = new();

    /// <summary>
    /// Top applications by usage time (sorted descending).
    /// </summary>
    public List<ProcessUsageDto> TopApplications { get; set; } = new();

    /// <summary>
    /// Average session duration in seconds.
    /// </summary>
    public long AverageSessionDurationSeconds
    {
        get => TotalSessions > 0 ? TotalUsageSeconds / TotalSessions : 0;
    }

    /// <summary>
    /// Human-readable total usage time.
    /// </summary>
    public string FormattedTotalUsage
    {
        get => FormatDuration(TotalUsageSeconds);
    }

    /// <summary>
    /// Human-readable average session duration.
    /// </summary>
    public string FormattedAverageSessionDuration
    {
        get => FormatDuration(AverageSessionDurationSeconds);
    }

    /// <summary>
    /// Formats duration in seconds to human-readable format (e.g., "2h 30m 45s").
    /// </summary>
    private static string FormatDuration(long seconds)
    {
        var hours = seconds / 3600;
        var minutes = (seconds % 3600) / 60;
        var secs = seconds % 60;

        if (hours > 0)
        {
            return $"{hours}h {minutes}m {secs}s";
        }
        else if (minutes > 0)
        {
            return $"{minutes}m {secs}s";
        }
        else
        {
            return $"{secs}s";
        }
    }
}

/// <summary>
/// DTO for process usage information.
/// </summary>
public class ProcessUsageDto
{
    /// <summary>
    /// Name of the process.
    /// </summary>
    public string ProcessName { get; set; } = string.Empty;

    /// <summary>
    /// Total duration in seconds.
    /// </summary>
    public long DurationSeconds { get; set; }

    /// <summary>
    /// Number of sessions for this process.
    /// </summary>
    public int SessionCount { get; set; }

    /// <summary>
    /// Percentage of total usage time (0-100).
    /// </summary>
    public decimal PercentageOfTotal { get; set; }

    /// <summary>
    /// Human-readable duration.
    /// </summary>
    public string FormattedDuration
    {
        get => FormatDuration(DurationSeconds);
    }

    /// <summary>
    /// Formats duration in seconds to human-readable format.
    /// </summary>
    private static string FormatDuration(long seconds)
    {
        var hours = seconds / 3600;
        var minutes = (seconds % 3600) / 60;
        var secs = seconds % 60;

        if (hours > 0)
        {
            return $"{hours}h {minutes}m {secs}s";
        }
        else if (minutes > 0)
        {
            return $"{minutes}m {secs}s";
        }
        else
        {
            return $"{secs}s";
        }
    }
}

/// <summary>
/// Interface for daily usage report generation.
/// </summary>
public interface IDailyReportService
{
    /// <summary>
    /// Generates a daily usage report for the specified date and user.
    /// </summary>
    /// <param name="reportDate">Date to generate report for.</param>
    /// <param name="userId">User ID to filter results.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Daily usage report with aggregated statistics.</returns>
    Task<DailyUsageReportDto> GenerateDailyReportAsync(
        DateOnly reportDate,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates daily usage reports for a date range.
    /// </summary>
    /// <param name="startDate">Start date (inclusive).</param>
    /// <param name="endDate">End date (inclusive).</param>
    /// <param name="userId">User ID to filter results.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of daily reports for each day in range.</returns>
    Task<List<DailyUsageReportDto>> GenerateDateRangeReportAsync(
        DateOnly startDate,
        DateOnly endDate,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets top N applications by usage time for a date range.
    /// </summary>
    /// <param name="startDate">Start date (inclusive).</param>
    /// <param name="endDate">End date (inclusive).</param>
    /// <param name="userId">User ID to filter results.</param>
    /// <param name="topCount">Number of top applications to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of top applications sorted by usage time (descending).</returns>
    Task<List<ProcessUsageDto>> GetTopApplicationsAsync(
        DateOnly startDate,
        DateOnly endDate,
        string userId,
        int topCount = 10,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Service to generate daily usage reports from tracked application sessions.
/// Aggregates session data by application and provides statistical insights.
/// </summary>
public class DailyReportService : IDailyReportService
{
    private readonly ILogger<DailyReportService> _logger;
    private readonly IUsageRepository _usageRepository;

    public DailyReportService(
        ILogger<DailyReportService> logger,
        IUsageRepository usageRepository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _usageRepository = usageRepository ?? throw new ArgumentNullException(nameof(usageRepository));
    }

    /// <summary>
    /// Generates a daily usage report for the specified date and user.
    /// </summary>
    public async Task<DailyUsageReportDto> GenerateDailyReportAsync(
        DateOnly reportDate,
        string userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Generating daily report for {ReportDate}, User: {UserId}", reportDate, userId);

            // Retrieve all sessions for the date
            var sessions = await _usageRepository.GetSessionsByDateRangeAsync(
                reportDate,
                reportDate,
                userId,
                cancellationToken);

            // Retrieve aggregated durations by process
            var aggregatedDurations = await _usageRepository.GetAggregatedDurationByProcessAsync(
                reportDate,
                reportDate,
                userId,
                cancellationToken);

            // Build the report
            var report = new DailyUsageReportDto
            {
                ReportDate = reportDate,
                UserId = userId,
                TotalUsageSeconds = sessions.Sum(s => s.DurationSeconds),
                TotalSessions = sessions.Count,
                UsageByProcess = aggregatedDurations
            };

            // Calculate per-process statistics
            var topApplications = new List<ProcessUsageDto>();
            
            foreach (var process in aggregatedDurations.OrderByDescending(x => x.Value))
            {
                var processSessions = sessions.Where(s => s.ProcessName == process.Key).Count();
                var percentage = report.TotalUsageSeconds > 0
                    ? (decimal)process.Value / report.TotalUsageSeconds * 100
                    : 0;

                topApplications.Add(new ProcessUsageDto
                {
                    ProcessName = process.Key,
                    DurationSeconds = process.Value,
                    SessionCount = processSessions,
                    PercentageOfTotal = Math.Round(percentage, 2)
                });
            }

            report.TopApplications = topApplications;

            _logger.LogInformation(
                "Generated daily report for {ReportDate}, User: {UserId}. Total sessions: {SessionCount}, Total usage: {TotalUsage}s, Applications: {AppCount}",
                reportDate,
                userId,
                report.TotalSessions,
                report.TotalUsageSeconds,
                aggregatedDurations.Count);

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating daily report for {ReportDate}, User: {UserId}", reportDate, userId);
            throw;
        }
    }

    /// <summary>
    /// Generates daily usage reports for a date range.
    /// </summary>
    public async Task<List<DailyUsageReportDto>> GenerateDateRangeReportAsync(
        DateOnly startDate,
        DateOnly endDate,
        string userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Generating date range report from {StartDate} to {EndDate}, User: {UserId}",
                startDate,
                endDate,
                userId);

            var reports = new List<DailyUsageReportDto>();
            var currentDate = startDate;

            while (currentDate <= endDate)
            {
                var report = await GenerateDailyReportAsync(currentDate, userId, cancellationToken);
                reports.Add(report);
                currentDate = currentDate.AddDays(1);
            }

            _logger.LogInformation(
                "Generated {ReportCount} daily reports for date range {StartDate} to {EndDate}, User: {UserId}",
                reports.Count,
                startDate,
                endDate,
                userId);

            return reports;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error generating date range report from {StartDate} to {EndDate}, User: {UserId}",
                startDate,
                endDate,
                userId);
            throw;
        }
    }

    /// <summary>
    /// Gets top N applications by usage time for a date range.
    /// </summary>
    public async Task<List<ProcessUsageDto>> GetTopApplicationsAsync(
        DateOnly startDate,
        DateOnly endDate,
        string userId,
        int topCount = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Getting top {TopCount} applications from {StartDate} to {EndDate}, User: {UserId}",
                topCount,
                startDate,
                endDate,
                userId);

            // Retrieve all sessions for the date range
            var sessions = await _usageRepository.GetSessionsByDateRangeAsync(
                startDate,
                endDate,
                userId,
                cancellationToken);

            // Retrieve aggregated durations by process
            var aggregatedDurations = await _usageRepository.GetAggregatedDurationByProcessAsync(
                startDate,
                endDate,
                userId,
                cancellationToken);

            // Calculate total usage for percentage calculations
            var totalUsage = sessions.Sum(s => s.DurationSeconds);

            // Build process usage statistics
            var applications = new List<ProcessUsageDto>();
            
            foreach (var process in aggregatedDurations.OrderByDescending(x => x.Value).Take(topCount))
            {
                var processSessions = sessions.Where(s => s.ProcessName == process.Key).Count();
                var percentage = totalUsage > 0
                    ? (decimal)process.Value / totalUsage * 100
                    : 0;

                applications.Add(new ProcessUsageDto
                {
                    ProcessName = process.Key,
                    DurationSeconds = process.Value,
                    SessionCount = processSessions,
                    PercentageOfTotal = Math.Round(percentage, 2)
                });
            }

            _logger.LogInformation(
                "Retrieved top {TopCount} applications from {StartDate} to {EndDate}, User: {UserId}. Total unique apps: {TotalApps}",
                applications.Count,
                startDate,
                endDate,
                userId,
                aggregatedDurations.Count);

            return applications;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error getting top applications from {StartDate} to {EndDate}, User: {UserId}",
                startDate,
                endDate,
                userId);
            throw;
        }
    }
}
