# Performance Remediation: Milestone 4 N+1 Query Issue

**Issue**: Critical performance deficiency in `DailyReportService` reporting methods
**Scope**: M4.S6 (Generate Daily Report)
**Priority**: CRITICAL (Must fix before production)

---

## Issue Analysis

### Current Implementation Problems

**Problem 1: Duplicate Query Execution**

File: `src/Services/DailyReportService.cs` lines 242-262 (GenerateDailyReportAsync method)

```csharp
// Query 1: Retrieve all sessions
var sessions = await _usageRepository.GetSessionsByDateRangeAsync(
    reportDate,
    reportDate,
    userId,
    cancellationToken);

// Query 2: Retrieve aggregated durations (DUPLICATE AGGREGATION)
var aggregatedDurations = await _usageRepository.GetAggregatedDurationByProcessAsync(
    reportDate,
    reportDate,
    userId,
    cancellationToken);
```

Both queries execute the same filtering predicates:
- WHERE SessionDate = reportDate
- WHERE UserId = userId

The second query performs GroupBy and Sum at the database layer, but the first query retrieves full session objects that will be processed again in memory.

**Problem 2: Client-Side N Aggregation**

Continuing in the same method (lines 252-262):

```csharp
foreach (var process in aggregatedDurations.OrderByDescending(x => x.Value))
{
    // This queries the in-memory sessions collection N times (N = number of processes)
    var processSessions = sessions.Where(s => s.ProcessName == process.Key).Count();
    var percentage = report.TotalUsageSeconds > 0
        ? (decimal)process.Value / report.TotalUsageSeconds * 100
        : 0;

    topApplications.Add(new ProcessUsageDto
    {
        ProcessName = process.Key,
        DurationSeconds = process.Value,
        SessionCount = processSessions,  // COMPUTED FROM MEMORY COLLECTION
        PercentageOfTotal = Math.Round(percentage, 2)
    });
}
```

The code:
1. Iterates through each process in aggregatedDurations (O(n) where n = number of processes)
2. For each process, filters the entire sessions list (O(m) where m = number of sessions)
3. Time complexity: O(n*m)

**Example Scenario**:
- 1000 sessions for the day
- 25 unique applications
- Time complexity: 1000 * 25 = 25,000 iterations for session count calculation

**Problem 3: Redundant Total Usage Calculation**

```csharp
var report = new DailyUsageReportDto
{
    ReportDate = reportDate,
    UserId = userId,
    TotalUsageSeconds = sessions.Sum(s => s.DurationSeconds),  // Full list iteration
    TotalSessions = sessions.Count,                             // Full list iteration
    UsageByProcess = aggregatedDurations
};
```

The `sessions.Sum()` iterates the entire collection. With database-side aggregation already performed, this is redundant computation.

---

## Root Cause Analysis

### Query Design Flaw

The `IUsageRepository` interface defines two separate operations for inherently related data:

```csharp
Task<List<AppUsageSession>> GetSessionsByDateRangeAsync(/* ... */);
Task<Dictionary<string, long>> GetAggregatedDurationByProcessAsync(/* ... */);
```

The client (`DailyReportService`) assumes both methods can be called independently, forcing the composition of their results in memory. This creates impedance mismatch between relational query model and object-oriented consumption model.

---

## Solution 1: Database-Side Aggregation DTO

### Approach: Single Query with Aggregated Result

Create a new repository method that returns pre-aggregated session data with session counts:

**File**: `src/Data/IUsageRepository.cs`

Add new interface method:
```csharp
/// <summary>
/// Retrieves aggregated session statistics by process for a date range.
/// Returns both total duration and session count per process in a single operation.
/// </summary>
/// <param name="startDate">Start date (inclusive).</param>
/// <param name="endDate">End date (inclusive).</param>
/// <param name="userId">User ID filter.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>Dictionary mapping process name to aggregated statistics.</returns>
Task<Dictionary<string, SessionAggregateDto>> GetSessionAggregatesByProcessAsync(
    DateOnly? startDate = null,
    DateOnly? endDate = null,
    string? userId = null,
    CancellationToken cancellationToken = default);
```

Create aggregation DTO:
```csharp
/// <summary>
/// Aggregated session statistics per process.
/// </summary>
public class SessionAggregateDto
{
    public string ProcessName { get; set; } = string.Empty;
    public long TotalDurationSeconds { get; set; }
    public int SessionCount { get; set; }
    public long TotalUsageSeconds { get; set; }  // Sum of all processes
}
```

**File**: `src/Data/UsageRepository.cs`

Implement the new method:
```csharp
/// <summary>
/// Retrieves aggregated session statistics by process.
/// Single database query with GROUP BY aggregation.
/// </summary>
public async Task<Dictionary<string, SessionAggregateDto>> GetSessionAggregatesByProcessAsync(
    DateOnly? startDate = null,
    DateOnly? endDate = null,
    string? userId = null,
    CancellationToken cancellationToken = default)
{
    var query = _context.AppUsageSessions.AsNoTracking();

    if (startDate.HasValue)
    {
        query = query.Where(s => s.SessionDate >= startDate.Value);
    }

    if (endDate.HasValue)
    {
        query = query.Where(s => s.SessionDate <= endDate.Value);
    }

    if (!string.IsNullOrEmpty(userId))
    {
        query = query.Where(s => s.UserId == userId);
    }

    // Single database operation with GROUP BY aggregation
    var aggregation = await query
        .GroupBy(s => s.ProcessName)
        .Select(g => new 
        { 
            ProcessName = g.Key, 
            TotalDuration = g.Sum(s => s.DurationSeconds),
            SessionCount = g.Count()
        })
        .ToListAsync(cancellationToken);

    // Calculate total duration across all processes
    var totalDuration = aggregation.Sum(a => a.TotalDuration);

    var result = aggregation.ToDictionary(
        x => x.ProcessName,
        x => new SessionAggregateDto
        {
            ProcessName = x.ProcessName,
            TotalDurationSeconds = x.TotalDuration,
            SessionCount = x.SessionCount,
            TotalUsageSeconds = totalDuration
        });

    _logger.LogDebug(
        "Retrieved aggregates for {ProcessCount} processes, Date range: {StartDate} to {EndDate}, User: {UserId}",
        result.Count,
        startDate,
        endDate,
        userId ?? "All");

    return result;
}
```

**Refactored DailyReportService**:

```csharp
public async Task<DailyUsageReportDto> GenerateDailyReportAsync(
    DateOnly reportDate,
    string userId,
    CancellationToken cancellationToken = default)
{
    try
    {
        _logger.LogInformation("Generating daily report for {ReportDate}, User: {UserId}", reportDate, userId);

        // SINGLE QUERY - Get all aggregated data
        var aggregates = await _usageRepository.GetSessionAggregatesByProcessAsync(
            reportDate,
            reportDate,
            userId,
            cancellationToken);

        // Build report from pre-aggregated data
        var report = new DailyUsageReportDto
        {
            ReportDate = reportDate,
            UserId = userId,
            TotalUsageSeconds = aggregates.Values.FirstOrDefault()?.TotalUsageSeconds ?? 0,
            TotalSessions = aggregates.Values.Sum(a => a.SessionCount),
            UsageByProcess = aggregates.ToDictionary(x => x.Key, x => x.Value.TotalDurationSeconds)
        };

        // Build TopApplications from aggregates (O(n) operation, not O(n*m))
        var topApplications = aggregates.Values
            .OrderByDescending(a => a.TotalDurationSeconds)
            .Select(a => new ProcessUsageDto
            {
                ProcessName = a.ProcessName,
                DurationSeconds = a.TotalDurationSeconds,
                SessionCount = a.SessionCount,
                PercentageOfTotal = report.TotalUsageSeconds > 0
                    ? Math.Round((decimal)a.TotalDurationSeconds / report.TotalUsageSeconds * 100, 2)
                    : 0
            })
            .ToList();

        report.TopApplications = topApplications;

        _logger.LogInformation(
            "Generated daily report for {ReportDate}, User: {UserId}. Total sessions: {SessionCount}, Total usage: {TotalUsage}s, Applications: {AppCount}",
            reportDate,
            userId,
            report.TotalSessions,
            report.TotalUsageSeconds,
            aggregates.Count);

        return report;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error generating daily report for {ReportDate}, User: {UserId}", reportDate, userId);
        throw;
    }
}
```

### Performance Improvement

**Before**:
- Query 1: SELECT * FROM AppUsageSessions WHERE SessionDate = ? AND UserId = ? (1000 rows returned)
- Query 2: SELECT ProcessName, SUM(DurationSeconds) FROM AppUsageSessions WHERE SessionDate = ? AND UserId = ? GROUP BY ProcessName (25 rows)
- Memory: O(n*m) = 1000 * 25 = 25,000 iterations for session count

**After**:
- Single Query: SELECT ProcessName, SUM(DurationSeconds), COUNT(*) FROM AppUsageSessions WHERE SessionDate = ? AND UserId = ? GROUP BY ProcessName (25 rows)
- Memory: O(n) = 25 iterations for report construction

**Result**: 98% reduction in database round trips, 100x reduction in memory iterations

---

## Solution 2: Bulk Date-Range Aggregation

### Problem with Current Multi-Day Reporting

```csharp
public async Task<List<DailyUsageReportDto>> GenerateDateRangeReportAsync(
    DateOnly startDate,
    DateOnly endDate,
    string userId,
    CancellationToken cancellationToken = default)
{
    var reports = new List<DailyUsageReportDto>();
    var currentDate = startDate;

    while (currentDate <= endDate)
    {
        var report = await GenerateDailyReportAsync(currentDate, userId, cancellationToken);
        reports.Add(report);
        currentDate = currentDate.AddDays(1);
    }

    return reports;
}
```

For 30-day period:
- 30 separate database queries (one per day)
- 30 separate GroupBy aggregations
- Total: 30 * 25 = 750 database round trips (if 25 apps per day)

### Improved Implementation

**New Repository Method**:

```csharp
/// <summary>
/// Retrieves pre-aggregated daily statistics for a date range in a single query.
/// Groups by (SessionDate, ProcessName) for efficient multi-day reporting.
/// </summary>
public async Task<List<DailyAggregateDto>> GetDailyAggregatesByDateRangeAsync(
    DateOnly startDate,
    DateOnly endDate,
    string userId,
    CancellationToken cancellationToken = default)
{
    var query = _context.AppUsageSessions
        .Where(s => s.SessionDate >= startDate && s.SessionDate <= endDate && s.UserId == userId)
        .GroupBy(s => new { s.SessionDate, s.ProcessName })
        .Select(g => new DailyAggregateDto
        {
            SessionDate = g.Key.SessionDate,
            ProcessName = g.Key.ProcessName,
            TotalDurationSeconds = g.Sum(s => s.DurationSeconds),
            SessionCount = g.Count()
        })
        .OrderBy(d => d.SessionDate)
        .ThenByDescending(d => d.TotalDurationSeconds);

    return await query.ToListAsync(cancellationToken);
}
```

**DTO Definition**:

```csharp
public class DailyAggregateDto
{
    public DateOnly SessionDate { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public long TotalDurationSeconds { get; set; }
    public int SessionCount { get; set; }
}
```

**Refactored Method**:

```csharp
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

        // SINGLE QUERY - Get all data for date range grouped by date and process
        var dailyAggregates = await _usageRepository.GetDailyAggregatesByDateRangeAsync(
            startDate,
            endDate,
            userId,
            cancellationToken);

        // Group results by date for report construction
        var reportsByDate = dailyAggregates
            .GroupBy(a => a.SessionDate)
            .Select(dateGroup => {
                var totalUsage = dateGroup.Sum(a => a.TotalDurationSeconds);
                var topApplications = dateGroup
                    .OrderByDescending(a => a.TotalDurationSeconds)
                    .Select(a => new ProcessUsageDto
                    {
                        ProcessName = a.ProcessName,
                        DurationSeconds = a.TotalDurationSeconds,
                        SessionCount = a.SessionCount,
                        PercentageOfTotal = totalUsage > 0
                            ? Math.Round((decimal)a.TotalDurationSeconds / totalUsage * 100, 2)
                            : 0
                    })
                    .ToList();

                return new DailyUsageReportDto
                {
                    ReportDate = dateGroup.Key,
                    UserId = userId,
                    TotalUsageSeconds = totalUsage,
                    TotalSessions = dateGroup.Sum(a => a.SessionCount),
                    UsageByProcess = dateGroup.ToDictionary(a => a.ProcessName, a => a.TotalDurationSeconds),
                    TopApplications = topApplications
                };
            })
            .ToList();

        _logger.LogInformation(
            "Generated {ReportCount} daily reports for date range {StartDate} to {EndDate}, User: {UserId}",
            reportsByDate.Count,
            startDate,
            endDate,
            userId);

        return reportsByDate;
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
```

### Performance Improvement

**Before** (30-day range):
- 30 daily queries
- Each query: GROUP BY ProcessName
- Total database round trips: 30

**After** (30-day range):
- 1 bulk query
- Single GROUP BY (SessionDate, ProcessName)
- Total database round trips: 1

**Result**: 97% reduction in database round trips

---

## Implementation Checklist

- [ ] Add `SessionAggregateDto` class to `src/Data/` directory
- [ ] Add `DailyAggregateDto` class to `src/Data/` directory
- [ ] Implement `GetSessionAggregatesByProcessAsync()` in `UsageRepository`
- [ ] Implement `GetDailyAggregatesByDateRangeAsync()` in `UsageRepository`
- [ ] Update `IDailyReportService` method signatures (optional, for clarity)
- [ ] Refactor `GenerateDailyReportAsync()` to use new repository method
- [ ] Refactor `GenerateDateRangeReportAsync()` to use bulk aggregation
- [ ] Add database index: `CREATE INDEX IX_Sessions_Date_Process ON AppUsageSessions(SessionDate, ProcessName)`
- [ ] Remove redundant transaction wrapping in `SaveSessionAsync()` and `UpdateSessionAsync()`
- [ ] Execute performance regression test comparing query counts before/after
- [ ] Verify no functional change in report output (unit tests)

---

## Verification Strategy

### Query Count Validation

Add query logging to verify reduction:

```csharp
// Temporary: Enable EF Core query logging
builder.Services.AddLogging(config => 
{
    config.AddDebug()
        .SetMinimumLevel(LogLevel.Debug);
});

// Monitor logs for:
// Before: 30+ "Executed DbCommand" log entries
// After: 1 "Executed DbCommand" log entry per report generation
```

### Performance Benchmark

```csharp
var stopwatch = Stopwatch.StartNew();
var report = await _reportService.GenerateDateRangeReportAsync(
    DateOnly.FromDateTime(DateTime.Now.AddDays(-30)),
    DateOnly.FromDateTime(DateTime.Now),
    userId,
    CancellationToken.None);
stopwatch.Stop();

_logger.LogInformation("Report generation took {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
// Expected: < 100ms for 30-day range on SSD-backed SQLite
// Before: > 5000ms
```

---

## Risk Assessment

### Low Risk Changes
- Adding new repository methods (additive, backward compatible)
- Creating new DTOs (additive)

### Medium Risk Changes
- Refactoring existing service methods (semantic changes to implementation)
- Mitigation: Comprehensive unit tests before/after

### Testing Requirements
1. Unit tests for new repository methods
2. Integration tests for report generation
3. Output validation (same format, same results)
4. Performance regression test (query count assertions)

---

## Estimated Effort

- Add DTOs: 30 minutes
- Implement repository methods: 45 minutes
- Refactor service methods: 45 minutes
- Unit testing: 2 hours
- Integration testing: 1 hour
- **Total**: 5-6 hours

---

## Success Criteria

✅ Single database query for single-day report (currently 2 queries)
✅ Single database query for multi-day range (currently N queries where N = days)
✅ O(n) time complexity for aggregation instead of O(n*m)
✅ All existing unit tests pass
✅ New integration tests validate output correctness
✅ Performance improvement > 90% for typical use cases
