# App Time Tracker - Design & Architecture Guidelines

## Project Overview

App Time Tracker is a lightweight Windows system service that passively monitors and tracks application usage time. Built on .NET 8 Worker Service architecture with Windows API interop, EF Core, and SQLite persistence.

## Architecture Principles

### Core Principles
- **Minimal Overhead**: Non-intrusive background service with <1% CPU impact
- **Reliability**: Atomic database transactions, graceful shutdown, health monitoring
- **Observability**: Structured logging via Serilog, Event Log integration
- **Maintainability**: Clean separation of concerns, dependency injection, SOLID principles
- **Production-Ready**: Error recovery, service resilience, comprehensive health checks

### Technology Stack
- **.NET 8 Worker Service**: Lightweight, focused entrypoint with built-in dependency injection
- **Entity Framework Core 8**: ORM with SQLite provider for durable persistence
- **SQLite**: Self-contained database, single-file storage, minimal overhead
- **Serilog**: Structured logging with console and file sinks
- **Windows API Interop**: SetWinEventHook for focus detection, session event monitoring
- **Windows Service Manager (sc.exe)**: Native Windows service hosting
- **PowerShell Scripts**: Installation/uninstallation automation

### Architectural Layers

#### 1. Presentation & Installation
- **scripts/**: PowerShell installation/uninstallation scripts
- **Windows Service Manager**: sc.exe integration via Program.cs
- **Command-line Interface**: Service status, diagnostics

#### 2. Core Service
- **Program.cs**: Bootstrap, dependency injection, Serilog configuration
- **Worker.cs**: IHostedService implementation, main monitoring loop
- **Configuration/**: AppSettings, HealthCheckSettings, LoggingSettings classes

#### 3. Monitoring Services
- **SessionMonitorService**: Detects foreground window focus changes
- **FocusMonitorService**: Monitors session lock/unlock/logon/logoff events
- **HealthCheckService**: Periodic health monitoring with auto-recovery

#### 4. Data Persistence
- **AppDbContext**: Entity Framework Core DbContext
- **AppUsageSession**: Data model for tracked sessions
- **UsageRepository**: CRUD operations and queries
- **Migrations/**: EF Core database schema versioning

#### 5. Infrastructure
- **LoggingService**: Logging configuration and integration
- **DatabaseInitializer**: Schema creation, migration application
- **HealthMonitors**: SqliteHealthMonitor, WinEventHookHealthMonitor, MemoryHealthMonitor
- **RecoveryManager**: Graceful failure handling and restart logic

## Configuration Management

### Configuration Sources (in order of precedence)
1. **Command-line arguments** (if provided)
2. **Environment variables** (.env file or system environment)
3. **appsettings.json** (default configuration)
4. **appsettings.Development.json** (development overrides)

### Required Configuration
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=%ProgramData%\\AppTimeTracker\\apptracker.db"
  },
  "Service": {
    "PollingIntervalMs": 500,
    "SessionFlushIntervalMs": 30000,
    "GracefulShutdownTimeoutSeconds": 30
  },
  "LoggingSettings": {
    "MaxFileSizeBytes": 10000000,
    "RetainedFileCount": 5,
    "EventLogFallbackEnabled": true
  },
  "HealthCheckSettings": {
    "Enabled": true,
    "IntervalSeconds": 60
  }
}
```

### Environment Variables
All environment variables are documented in `.env.example`:
- `DB_PATH`: SQLite database location
- `LOG_PATH`: Log file directory
- `LOG_LEVEL`: Serilog minimum level (Debug, Information, Warning, Error, Fatal)
- `POLLING_INTERVAL_MS`: Focus detection polling interval (default 500ms)
- `SESSION_FLUSH_INTERVAL_MS`: Database flush interval (default 30000ms)

## Database Design

### Schema
- **AppUsageSessions**: Primary data table
  - Columns: Id, ProcessName, WindowTitle, ExecutablePath, UserId, StartTimeUtc, EndTimeUtc, SessionDate
  - Indexes: SessionDate, ProcessName, Composite (SessionDate, UserId)
  - Constraints: Foreign keys for referential integrity

### Data Integrity
- Atomic transactions for session creation/update
- Referential integrity via foreign keys
- Row-level timestamps (UTC for consistency)
- User isolation via UserId column

### Backup Strategy
- Daily rolling log files (configurable retention)
- SQLite database persists to ProgramData (Windows backup-safe location)
- WAL (Write-Ahead Logging) disabled for compatibility

## Logging & Observability

### Serilog Configuration
- **Console Sink**: Real-time logs during development
- **File Sink**: Persistent logs at `%ProgramData%\AppTimeTracker\Logs\`
  - Rolling interval: Daily
  - Max file size: 10MB (configurable)
  - Retention: 5 files (configurable)
- **Event Log Sink**: Windows Event Log for service events (warnings/errors)

### Log Levels
- **Debug**: Detailed internal state (disabled in production)
- **Information**: Service lifecycle, key operations
- **Warning**: Configuration issues, non-fatal errors
- **Error**: Service failures, DB errors
- **Fatal**: Unrecoverable errors

### Structured Logging
All logs include:
- Timestamp (ISO 8601 UTC)
- Level (Information, Warning, Error, etc.)
- Process ID, Thread ID
- Enriched context (service name, environment)
- Exception details (if applicable)

## Health Monitoring

### Health Checks
1. **SqliteHealthMonitor**: Verifies database connectivity
2. **WinEventHookHealthMonitor**: Checks session event hook status
3. **MemoryHealthMonitor**: Monitors process memory usage

### Recovery Strategy
- **Retry Logic**: Exponential backoff (initial 100ms, multiplier 2.0)
- **Auto-Recovery**: Attempts to restore failed components
- **Graceful Degradation**: Service continues with reduced functionality if non-critical component fails
- **Shutdown**: Triggers controlled shutdown if critical systems fail

### Health Status Enum
- `Healthy`: All systems operational
- `Degraded`: Non-critical component failed
- `Unhealthy`: Critical component failed

## Service Lifecycle

### Startup Sequence
1. Bootstrap logger (early logging)
2. Configure dependency injection
3. Create Windows Service wrapper
4. Initialize EF Core + SQLite context
5. Bind configuration sections
6. Configure Serilog from appsettings
7. Register core services (Session, Focus, Health monitors)
8. Apply EF Core migrations
9. Verify health checks
10. Start hosted services in order:
    - DatabaseInitializer (apply schema)
    - SessionMonitorService (start monitoring)
    - FocusMonitorService (start event hooks)
    - HealthCheckService (periodic health)
    - Worker (main loop)

### Shutdown Sequence
1. Listen on `IHostApplicationLifetime.ApplicationStopping`
2. Pause monitoring services
3. Flush pending data to database
4. Close database connections (commit pending transactions)
5. Shut down Serilog (flush remaining logs)
6. Release Win32 event hooks
7. Exit with status code

### Graceful Shutdown Configuration
- **Timeout**: 30 seconds (configurable via `GRACEFUL_SHUTDOWN_TIMEOUT_SECONDS`)
- **Implementation**: Via `HostOptions.ShutdownTimeout` in Program.cs

## Error Handling & Recovery

### Configuration Errors
- Missing required variables → Log warning, use safe default, continue
- Invalid paths → Auto-create directories, log info
- Invalid JSON → Log error, fail fast with clear message

### Database Errors
- Connection failure → Retry with exponential backoff (3 attempts)
- Migration failure → Detailed error message, halt startup
- Transaction rollback → Log details, retry operation
- Query failure → Log and propagate (caller decides handling)

### Service Errors
- Unhandled exception → Log Fatal, trigger graceful shutdown
- Win32 API failure → Fallback to polling, log warning
- Event Log source creation → Log warning if fails (may require elevation)

### Recovery Mechanisms
1. **Automatic Retry**: Database operations use retry policies
2. **Component Restart**: Failed services can be restarted by health monitor
3. **Fallback Mode**: If event hooks fail, use polling instead
4. **Safe Shutdown**: Always gracefully close resources

## Development Practices

### Code Organization
- **Namespaces**: `AppTimeTracker.*` hierarchy matching folder structure
- **Classes**: One class per file, named after responsibility
- **Methods**: Single responsibility, max 20 lines preferred
- **Comments**: Document WHY, not WHAT; use XML docs for public APIs

### Dependency Injection
All services registered in Program.cs via `builder.Services`:
- Singletons: Logging, health monitors, configuration
- Scoped: Database context (per request)
- Transients: Stateless utilities

### Testing Strategy
- **Unit Tests**: Mock DbContext, test business logic
- **Integration Tests**: Real SQLite in-memory database
- **Service Tests**: Full stack with Windows API mocks
- **Health Checks**: Verify system state on startup

### Versioning
- **Assembly Version**: Set in AppTimeTracker.csproj
- **Product Version**: Displayed in logs
- **Database Migrations**: Timestamped, reversible (in theory)

## Security Considerations

### Data Protection
- **User Isolation**: Data stored per user (UserId column)
- **Access Control**: Database files owned by service account
- **Encryption at Rest**: Optional via SQLite pragma encryption_key (not implemented by default)
- **Encryption in Transit**: N/A (local only)

### Service Security
- **Account**: Runs as LocalService (limited privileges)
- **UAC**: Installer requires admin elevation
- **Permissions**: Database/log directories have restricted ACLs
- **Event Log**: Writes to Application log (standard Windows location)

### Sensitive Data
- **Passwords**: None stored (service account credentials in Windows)
- **API Keys**: None used (local-only operation)
- **Window Titles**: Stored in database (may contain sensitive info - consider hashing in future)

## Deployment & Operations

### Installation
```powershell
# Run as Administrator
cd scripts
.\Install-Service.ps1
```

### Service Commands
```powershell
# Check status
Get-Service -Name AppTimeTracker

# Start/stop
Start-Service -Name AppTimeTracker
Stop-Service -Name AppTimeTracker

# View logs
Get-EventLog -LogName Application -Source AppTimeTracker -Newest 10

# Check process
Get-Process | Where-Object { $_.ProcessName -eq "AppTimeTracker" }
```

### Troubleshooting
1. **Service won't start**: Check Event Log for errors, verify .NET 8 installed
2. **Database locked**: Restart service, check for orphaned processes
3. **High memory usage**: Check health monitor logs, increase memory threshold
4. **Missing logs**: Verify log path exists, check permissions

### Monitoring
- **Event Log**: Application log, source "AppTimeTracker"
- **File Logs**: `%ProgramData%\AppTimeTracker\Logs\apptracker-*.log`
- **Database**: Check AppUsageSessions table row count, last activity date

## Future Enhancements

### Planned Features
- [ ] Seq integration for centralized logging
- [ ] Sentry error aggregation
- [ ] REST API for data querying
- [ ] Windows Event Log event ID customization
- [ ] Database encryption (AES-256)
- [ ] Performance profiling/benchmarking
- [ ] Multi-user tracking (enterprise mode)
- [ ] Data export (CSV/JSON reports)

### Architecture Evolution
- [ ] Modular plugin system for custom integrations
- [ ] Pub/sub event bus (reduce coupling)
- [ ] Async/await throughout (reduce blocking operations)
- [ ] gRPC API layer (alternative to REST)
- [ ] Containerization (Docker/Windows containers)

## References & Links

- [.NET 8 Worker Service Documentation](https://learn.microsoft.com/en-us/dotnet/core/extensions/workers)
- [Entity Framework Core SQLite Provider](https://learn.microsoft.com/en-us/ef/core/providers/sqlite)
- [Serilog Structured Logging](https://serilog.net/)
- [Windows API SetWinEventHook](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwineventhook)
- [Windows Service Documentation](https://learn.microsoft.com/en-us/dotnet/framework/windows-services/introduction)
- [SQLite Best Practices](https://www.sqlite.org/bestpractice.html)

## Maintenance Checklist

- [ ] Run build weekly: `dotnet build -c Release`
- [ ] Test service installation: Run Install-Service.ps1 on clean Windows
- [ ] Review logs for errors: Check Event Log weekly
- [ ] Update dependencies: `dotnet list package --outdated`
- [ ] Database maintenance: Periodic vacuum (if WAL enabled)
- [ ] Performance baseline: Track startup time, memory usage trends
