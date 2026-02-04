# Milestone 4: Usage Monitoring - Completion Summary

## Overview
**Milestone 4** is now **COMPLETE**. This milestone implements the core usage monitoring system for AppTimeTracker, enabling the application to track active application focus changes and record application usage intervals with full persistence and health monitoring.

## Completed Stories

### ✅ M4.S1: Focus & Session (1h)
**Status**: DONE

Tracks active application focus changes and user session start/stop events. The implementation:
- **FocusMonitorService**: Uses `SetWinEventHook` (Windows API) to monitor `EVENT_SYSTEM_FOREGROUND` events
- **SessionMonitorService**: Detects session lock/unlock and disconnect/reconnect events via Windows Session Change Notifications
- **Worker**: Orchestrates focus changes to start/stop tracking sessions
- **Event Args**: Provides `FocusChangeEventArgs` with process info (name, PID, executable path, window title)

**Files**:
- `src/Services/FocusMonitorService.cs`
- `src/Services/SessionMonitorService.cs`
- `src/Services/IFocusMonitorService.cs`
- `src/Services/ISessionMonitorService.cs`
- `src/Worker.cs`

---

### ✅ M4.S2: Persistence & Health (2h)
**Status**: DONE

Persists usage data to SQLite via EF Core with health status endpoints. The implementation:
- **AppDbContext**: Configured with strategic indexes on `SessionDate`, `ProcessName`, and composite keys for efficient queries
- **UsageRepository**: Implements CRUD operations with atomic transactions for data integrity
- **AppUsageSession Model**: Entity with properties for process tracking, timestamps, duration calculation, and user association
- **Database Indexes**: 
  - `IX_Sessions_Date`: Fast date-range queries
  - `IX_Sessions_Process`: Process grouping and aggregation
  - `IX_Sessions_Date_User`: Multi-user support with efficient filtering

**Files**:
- `src/Data/AppDbContext.cs`
- `src/Data/UsageRepository.cs`
- `src/Data/IUsageRepository.cs`
- `src/Models/AppUsageSession.cs`
- `src/Migrations/20260119040544_InitialCreate.cs`

---

### ✅ M4.S3: App Logging (1h)
**Status**: DONE

Captures and persists application logs with contextual data. The implementation:
- **AppLog Model**: Stores structured logs with severity, component source, timestamps, stack traces, and metadata
- **AppLogRepository**: Full CRUD with search capabilities by severity, date range, and component
- **LoggingService**: Manages async log persistence with background queue processing and graceful flush on shutdown
- **Serilog Integration**: Structured logging with file rotation, Windows Event Log fallback, and context enrichment
- **Migration**: Creates AppLogs table with optimized indexes for timestamp, severity, and correlation ID

**Files**:
- `src/Models/AppLog.cs`
- `src/Data/AppLogRepository.cs`
- `src/Services/LoggingService.cs`
- `src/Migrations/20260204_AddLoggingAndHealth.cs`

---

### ✅ M4.S4: Health Monitor (2h)
**Status**: DONE

Monitors and exposes health status of the logger and health checks. The implementation:
- **HealthCheck Model**: Stores health check results with database connectivity, hook status, memory usage, and timestamp
- **HealthCheckService**: Hosted service that runs periodic health checks (default: every 10 seconds)
- **Health Monitors**:
  - `SqliteHealthMonitor`: Validates database connectivity
  - `WinEventHookHealthMonitor`: Checks if FocusMonitorService hook is active
  - `MemoryHealthMonitor`: Tracks memory usage (warning at 500MB, critical at 1GB)
- **Alerting**: Logs warnings and errors when health status degrades
- **Migration**: Creates HealthChecks table with indexes for timestamp and status

**Files**:
- `src/Models/HealthCheck.cs`
- `src/Models/HealthCheckResult.cs`
- `src/Data/HealthCheckRepository.cs`
- `src/Services/HealthCheckService.cs`
- `src/Services/HealthMonitors/SqliteHealthMonitor.cs`
- `src/Services/HealthMonitors/WinEventHookHealthMonitor.cs`
- `src/Services/HealthMonitors/MemoryHealthMonitor.cs`

---

### ✅ M4.S5: Record App Usage (1h)
**Status**: DONE

Records application usage intervals (active window durations) for each foreground process. The implementation:
- **AppUsageSession Model**: Tracks process name, executable path, window title, start/end times, duration, session date, and user ID
- **Worker Integration**: 
  - Subscribes to `FocusChanged` events from FocusMonitorService
  - Creates new AppUsageSession when focus changes to a new application
  - Calculates duration and persists completed sessions
  - Respects minimum 1-second session filter to avoid noise
  - Pauses tracking during session lock/disconnect
- **Session Persistence**:
  - Sessions are persisted on focus change
  - Active sessions are flushed on service shutdown
  - Atomic transactions ensure data integrity
- **Usage Repository**: Provides aggregation methods for daily/weekly/monthly reports

**Files**:
- `src/Models/AppUsageSession.cs`
- `src/Data/UsageRepository.cs`
- `src/Data/IUsageRepository.cs`
- `src/Worker.cs`

---

### ✅ M4.S6: Generate Daily Report (2h)
**Status**: DONE

Generates daily usage reports aggregating session data by application and total usage time. The implementation:
- **DailyReportService**: Full-featured reporting engine with three main methods:
  1. `GenerateDailyReportAsync()`: Single-day report with aggregated stats
  2. `GenerateDateRangeReportAsync()`: Multi-day reports for trends analysis
  3. `GetTopApplicationsAsync()`: Top N applications by usage time
  
- **DailyUsageReportDto**: Report DTO with:
  - Report date, user ID
  - Total usage time and session count
  - Usage breakdown by process (dictionary)
  - Top applications (sorted descending)
  - Average session duration
  - Human-readable formatted durations (e.g., "2h 30m 45s")

- **ProcessUsageDto**: Per-application statistics:
  - Process name and duration
  - Session count and percentage of total
  - Human-readable formatted duration

- **Features**:
  - Leverages indexed queries from UsageRepository for performance
  - Aggregates data by process name
  - Calculates percentage-of-total metrics
  - Supports date ranges and per-user filtering
  - Provides human-readable duration formatting
  - Comprehensive error logging and monitoring

**Files**:
- `src/Services/DailyReportService.cs` (NEW)
- Registered in `src/Program.cs` as scoped service

---

## Architecture Summary

### Data Model
```
AppUsageSession
├── Id (PK)
├── ProcessName (indexed)
├── ExecutablePath
├── WindowTitle
├── StartTimeUtc
├── EndTimeUtc
├── DurationSeconds
├── SessionDate (indexed)
└── UserId (indexed)
```

### Processing Flow
```
FocusMonitorService (detects focus changes)
    ↓
Worker (orchestrates tracking)
    ↓
Creates AppUsageSession
    ↓
PersistenceService / UsageRepository (saves to SQLite)
    ↓
DailyReportService (generates reports)
```

### Query Performance
- Date-range queries: Indexed on `SessionDate`
- Process aggregation: Indexed on `ProcessName`
- Multi-user support: Composite index on `(SessionDate, UserId)`
- Report generation: Uses non-tracked queries for performance

---

## Configuration

### appsettings.json
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  },
  "LoggingSettings": {
    "MaxFileSizeBytes": 10485760,
    "RetainedFileCount": 7,
    "EventLogFallbackEnabled": true
  },
  "HealthCheckSettings": {
    "IntervalSeconds": 10,
    "MemoryWarningMB": 500,
    "MemoryCriticalMB": 1000
  },
  "TelemetrySettings": {
    "EventBufferSize": 1000,
    "FlushIntervalMs": 5000,
    "RetryCount": 3,
    "RetryDelayMs": 100
  },
  "SessionMonitoring": {
    "PauseOnLock": true
  }
}
```

---

## Testing & Verification

### Manual Testing Steps
1. Install and start the service
2. Open various applications (browser, text editor, IDE)
3. Switch focus between applications
4. Check the database:
   ```sql
   SELECT ProcessName, COUNT(*) as Sessions, SUM(DurationSeconds) as TotalSeconds
   FROM AppUsageSessions
   GROUP BY ProcessName
   ORDER BY TotalSeconds DESC;
   ```
5. Query reports programmatically via `IDailyReportService`

### Database Verification
```sql
-- Check sessions table
SELECT COUNT(*) FROM AppUsageSessions;

-- View usage by process
SELECT ProcessName, COUNT(*) as Sessions, SUM(DurationSeconds) as TotalSeconds
FROM AppUsageSessions
GROUP BY ProcessName
ORDER BY TotalSeconds DESC;

-- View today's usage
SELECT * FROM AppUsageSessions
WHERE SessionDate = DATE('now')
ORDER BY StartTimeUtc DESC;
```

---

## Integration Points

### Service Registration (Program.cs)
```csharp
// Usage tracking repositories
builder.Services.AddScoped<IUsageRepository, UsageRepository>();

// Report generation
builder.Services.AddScoped<IDailyReportService, DailyReportService>();

// Background persistence
builder.Services.AddSingleton<PersistenceService>();
```

### Usage in Code
```csharp
// Inject and use
private readonly IDailyReportService _reportService;

// Generate daily report
var report = await _reportService.GenerateDailyReportAsync(
    DateOnly.FromDateTime(DateTime.Now),
    userId,
    cancellationToken);

// Get top 5 apps
var topApps = await _reportService.GetTopApplicationsAsync(
    startDate,
    endDate,
    userId,
    topCount: 5,
    cancellationToken);
```

---

## Performance Characteristics

| Operation | Time Complexity | Optimizations |
|-----------|-----------------|----------------|
| Record session | O(1) | Indexed insert on (SessionDate, UserId) |
| Daily report | O(n) | Single pass, leverages DB aggregation |
| Date range report | O(n*d) | n = sessions, d = days in range |
| Top apps | O(n log k) | k = topCount, uses sorted GroupBy |

---

## Logging & Monitoring

### Log Levels
- **Debug**: Detailed session tracking, buffer operations
- **Information**: Report generation, session persistence
- **Warning**: Health status degradation, duplicate events
- **Error**: Database failures, invalid data

### Health Checks
- Database connectivity: Every 10 seconds
- Memory usage: Every 10 seconds
- Hook status: Monitored within health checks
- Alerts logged to AppLogs table and Windows Event Log

---

## Next Steps (Milestone 5+)

- **M5.S1**: Log Notification Entry (extend AppLog for notifications)
- **M5.S2**: Show Desktop Toast (integrate notification UI)
- **M5.S3**: Persist Settings (user preferences)
- **M5.S4**: Toggle Monitoring (pause/resume tracking)
- **M6**: Graceful shutdown and update support
- **M7**: CI/CD, documentation, and deployment

---

## Files Modified/Created

### New Files
- `src/Services/DailyReportService.cs` - Report generation service

### Modified Files
- `src/Program.cs` - Registered DailyReportService dependency

### Existing (Already Complete)
- `src/Worker.cs` - Session tracking orchestration
- `src/Services/FocusMonitorService.cs` - Focus detection
- `src/Services/SessionMonitorService.cs` - Session state monitoring
- `src/Data/UsageRepository.cs` - Data persistence
- `src/Models/AppUsageSession.cs` - Domain model
- `src/Data/AppDbContext.cs` - EF Core configuration
- Database migrations for sessions, focus events, logs, health checks

---

## Acceptance Criteria Met

✅ Focus changes are tracked and recorded
✅ Usage intervals are persisted to SQLite
✅ Daily reports can be generated with aggregated stats
✅ Top applications can be ranked by usage time
✅ Date range reporting is supported
✅ Health status of logger and services is monitored
✅ Sessions respect lock/unlock state
✅ Minimum session duration filter (1 second) is applied
✅ Graceful shutdown persists active sessions
✅ All data is user-scoped and timezone-aware (UTC)

---

**Milestone 4 is complete and ready for integration testing.**
