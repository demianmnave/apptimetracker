# App Time Tracker

A lightweight Windows system service that passively monitors and tracks application usage time for the signed-in user. The service runs as a background Windows service using .NET 8 Worker Service architecture, leveraging Win32 API interop to detect foreground window focus changes and maintain comprehensive usage metrics in a local SQLite database.

## Overview

App Time Tracker delivers precise insights into daily application usage patterns without impacting system performance. The service:

- **Monitors Focus Changes**: Uses Windows API (`SetWinEventHook`) to detect when applications gain or lose focus
- **Tracks Session State**: Monitors Windows session events (lock/unlock/logon/logoff) to pause tracking appropriately
- **Persists Data**: Stores process names, window titles, and accumulated time to a local SQLite database with full atomic transaction support
- **Non-Intrusive**: Runs with minimal system overhead, suitable for continuous deployment on user machines
- **Lightweight**: Pure .NET 8 with no external dependencies beyond standard frameworks

## Prerequisites

### System Requirements

- **Operating System**: Windows 10, Windows 11, or Windows Server 2016 or later
- **.NET 8 Runtime**: [Download .NET 8](https://dotnet.microsoft.com/download/dotnet/8.0)
  - Verify installation: `dotnet --list-runtimes` should show `Microsoft.NETCore.App 8.x.x`
- **Administrator Privileges**: Required to:
  - Register the Windows service
  - Create Event Log sources
  - Set ACLs on ProgramData directories
  - Install/uninstall the service

### Permissions

- Service installation requires local administrator rights
- Service must run under an account with desktop interaction capability (typically `LocalSystem` or `LocalService`)
- Database file requires read/write access by the service account (automatically configured by installer)

## Quick Start

### 1. Build the Service

```bash
# From the repository root
dotnet publish -c Release -r win-x64 --self-contained false
```

The executable will be available at: `src/bin/Release/net8.0/win-x64/publish/AppTimeTracker.exe`

### 2. Install the Service

Open PowerShell as Administrator and run:

```powershell
# Navigate to the scripts directory
cd scripts

# Run the installer
.\Install-Service.ps1
```

**Expected Output:**
```
========================================
  AppTimeTracker Service Installer
========================================

[*] Checking .NET 8 Runtime...
[+] .NET 8 Runtime is installed.
[*] Validating service executable...
[+] Found service executable: ...
[*] Creating ProgramData directory...
[+] Created directory: C:\ProgramData\AppTimeTracker
...
[+] Service started successfully!

========================================
  Installation Complete
========================================

Service Name:     AppTimeTracker
Display Name:     App Time Tracker
Data Directory:   C:\ProgramData\AppTimeTracker
```

### 3. Verify Installation

```powershell
# Check service status
Get-Service -Name AppTimeTracker

# View recent log entries
Get-EventLog -LogName Application -Source AppTimeTracker -Newest 10
```

### 4. Uninstall the Service

To remove the service, open PowerShell as Administrator:

```powershell
# Navigate to scripts directory
cd scripts

# Run the uninstaller (keeps data by default)
.\Uninstall-Service.ps1

# Or remove data as well
.\Uninstall-Service.ps1 -RemoveData
```

## Architecture

### Component Overview

The service follows a layered architecture with clear separation of concerns:

```
┌─────────────────────────────────────────────────────────────┐
│                    Windows App Time Tracker                 │
│                  .NET 8 Worker Service Host                 │
└─────────────────────────────────────────────────────────────┘
                             │
                ┌────────────┼────────────┐
                │            │            │
         ┌──────▼──────┐  ┌──▼──────┐  ┌─▼──────────┐
         │   Focus     │  │ Session │  │  Worker    │
         │   Monitor   │  │ Monitor │  │  Service   │
         │   Service   │  │ Service │  │            │
         └──────┬──────┘  └────┬────┘  └─┬──────────┘
                │             │         │
                └─────────────┼─────────┘
                              │
                      ┌───────▼────────┐
                      │  UsageRepository
                      │  (EF Core + SQLite)
                      └────────┬────────┘
                               │
                      ┌────────▼────────┐
                      │  SQLite Database │
                      │ (AppUsageSessions)
                      └─────────────────┘
```

### Key Components

#### **FocusMonitorService** (`src/Services/FocusMonitorService.cs`)
- Uses `SetWinEventHook` (Win32 P/Invoke) to monitor `EVENT_SYSTEM_FOREGROUND` events
- Fires `FocusChanged` event when foreground window changes
- Retrieves process information: `ProcessName`, `ExecutablePath`, `WindowTitle`, `ProcessId`
- Handles elevated process limitations (shows as `Process_<PID>` when process info unavailable)
- **Event Driven**: Highly responsive, minimal polling overhead

#### **SessionMonitorService** (`src/Services/SessionMonitorService.cs`)
- Monitors Windows session state: Active, Locked, Disconnected
- Uses hidden message window to receive `WM_WTSSESSION_CHANGE` messages
- Tracks current user ID and session state
- Exposes `SessionStateChanged` event for pause/resume logic
- **Configuration**: Can be disabled via `SessionMonitoring:Enabled` setting

#### **Worker Service** (`src/Worker.cs`)
- Aggregates focus change events from `FocusMonitorService`
- Applies pause logic based on `SessionMonitorService` state
- Flushes accumulated session data to database at configurable intervals
- Handles graceful shutdown with transaction completion

#### **UsageRepository** (`src/Data/UsageRepository.cs`)
- Abstracts database access using Entity Framework Core
- Provides `SaveSessionAsync()` for atomic transaction persistence
- Provides `GetSessionSummaryAsync()` for usage aggregation queries
- Ensures data integrity with transactional commits

### Data Flow

1. **Focus Change Detection**
   - User switches applications
   - Windows triggers `EVENT_SYSTEM_FOREGROUND` event
   - `FocusMonitorService.WinEventProc()` is called
   - `FocusChanged` event is fired with process details

2. **Session Aggregation**
   - `Worker` receives `FocusChanged` event
   - Closes previous session (if any) with `EndTimeUtc` and `DurationSeconds`
   - Creates new session with `StartTimeUtc`
   - Accumulates sessions in memory

3. **Pause/Resume Logic**
   - `SessionMonitorService` detects lock/unlock
   - `Worker` respects pause state based on `SessionMonitoring:PauseOnLock` setting
   - Sessions not recorded when system is locked (if pausing enabled)

4. **Persistence**
   - At `Service:SessionFlushIntervalMs` intervals (default: 30 seconds)
   - `Worker` calls `UsageRepository.SaveSessionAsync()`
   - All accumulated sessions saved in atomic transaction
   - Database ensures referential integrity and consistency

### Database Schema

**AppUsageSessions Table:**

| Column | Type | Description |
|--------|------|-------------|
| `Id` | INT (PK, Auto) | Primary key |
| `ProcessName` | VARCHAR(255) | Process name (e.g., "chrome", "notepad") |
| `ExecutablePath` | VARCHAR(1024) | Full path to executable |
| `WindowTitle` | VARCHAR(1024) | Active window title during session |
| `StartTimeUtc` | DATETIME | Session start time (UTC) |
| `EndTimeUtc` | DATETIME | Session end time (UTC, NULL if active) |
| `DurationSeconds` | BIGINT | Total session duration in seconds |
| `SessionDate` | DATE | Session date for daily aggregation |
| `UserId` | VARCHAR(256) | Windows user ID (SID or username) |

**Indexes:**
- `(UserId, SessionDate)` - Fast daily summary queries
- `(ProcessName)` - Application usage trends

## Configuration

Configuration is defined in `src/appsettings.json`. All settings can be overridden via environment variables using double-underscore notation (e.g., `Service__PollingIntervalMs`).

### Database Connection

```json
"ConnectionStrings": {
  "DefaultConnection": "Data Source=%ProgramData%\\AppTimeTracker\\apptracker.db"
}
```

- **Default Location**: `C:\ProgramData\AppTimeTracker\apptracker.db`
- **Path Substitution**: `%ProgramData%` is expanded at runtime
- **Automatic Creation**: Database is created automatically on first run if missing
- **Permissions**: Service account must have read/write access (configured by installer)

### Service Polling

```json
"Service": {
  "PollingIntervalMs": 500,
  "SessionFlushIntervalMs": 30000,
  "GracefulShutdownTimeoutSeconds": 5
}
```

| Setting | Default | Description |
|---------|---------|-------------|
| `PollingIntervalMs` | 500 | How often to check for session state changes (milliseconds) |
| `SessionFlushIntervalMs` | 30000 | How often to flush accumulated sessions to database (milliseconds) |
| `GracefulShutdownTimeoutSeconds` | 5 | Time to wait for pending operations before forcing shutdown |

### Session Monitoring

```json
"SessionMonitoring": {
  "Enabled": true,
  "PauseOnLock": true
}
```

| Setting | Default | Description |
|---------|---------|-------------|
| `Enabled` | true | Enable/disable Windows session monitoring |
| `PauseOnLock` | true | Pause usage tracking when system is locked |

### Logging

```json
"Serilog": {
  "MinimumLevel": {
    "Default": "Information",
    "Override": {
      "Microsoft": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning",
      "System": "Warning"
    }
  }
}
```

**Log Destinations:**
1. **Console**: Real-time output (useful for debugging)
2. **Event Log**: Windows Application event log with source `AppTimeTracker`
3. **File**: Daily rolling logs in `C:\ProgramData\AppTimeTracker\Logs\`
   - File pattern: `apptracker-YYYYMMDD.log`
   - Retention: 30 days
   - Max file size: 10 MB

**Log Levels:**
- `Verbose`: Extremely detailed diagnostic information
- `Debug`: Detailed information for troubleshooting (focus changes, process details)
- `Information`: General informational messages (service startup, configuration)
- `Warning`: Warning conditions that don't prevent operation
- `Error`: Error conditions requiring attention
- `Fatal`: Unrecoverable errors

## Troubleshooting

### Service Fails to Start

**Symptoms:** Service appears in Services list but fails to start, or starts then immediately stops.

**Diagnosis:**
1. Check Event Viewer:
   ```powershell
   Get-EventLog -LogName Application -Source AppTimeTracker -Newest 20
   ```

2. Check service status:
   ```powershell
   Get-Service -Name AppTimeTracker
   ```

**Common Causes & Solutions:**

| Issue | Cause | Solution |
|-------|-------|----------|
| `.NET 8 Runtime not found` | .NET 8 not installed | Download and install [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) |
| `EventLog source could not be created` | Admin rights not available | Re-run installer as Administrator; service will still work without EventLog |
| `Database file in use` | Previous service instance still running | Stop service with `Stop-Service -Name AppTimeTracker -Force` |
| `ProgramData directory permission denied` | Incorrect ACLs | Re-run installer to fix permissions |

**Recovery Steps:**
```powershell
# 1. Stop the service
Stop-Service -Name AppTimeTracker -Force

# 2. Wait a moment for process to release resources
Start-Sleep -Seconds 3

# 3. Start the service again
Start-Service -Name AppTimeTracker

# 4. Check status
Get-Service -Name AppTimeTracker
```

### Database Access Errors

**Symptoms:** Errors about database file being locked or permission denied.

**Diagnosis:**
1. Verify database file exists:
   ```powershell
   Test-Path "C:\ProgramData\AppTimeTracker\apptracker.db"
   ```

2. Check file permissions:
   ```powershell
   (Get-Item "C:\ProgramData\AppTimeTracker").GetAccessControl() | Format-List
   ```

**Common Causes & Solutions:**

| Issue | Cause | Solution |
|-------|-------|----------|
| `Database is locked` | Multiple service instances running | Ensure only one instance; check Task Manager |
| `Permission denied` | Service account lacks rights | Reinstall service; installer configures ACLs |
| `File not found` | Database not created | Ensure `C:\ProgramData\AppTimeTracker` directory exists |

**Manual Fix:**
```powershell
# Verify directory structure
Get-ChildItem -Path "C:\ProgramData\AppTimeTracker" -Recurse

# Reset ACLs
$acl = Get-Acl "C:\ProgramData\AppTimeTracker"
Set-Acl -Path "C:\ProgramData\AppTimeTracker" -AclObject $acl
```

### Cannot Track Elevated Applications

**Symptoms:** Some applications (Administrator-elevated) show as `Process_<PID>` instead of process name.

**Cause:** Windows security boundaries prevent non-elevated processes from accessing info about elevated processes.

**Limitations:**
- Service cannot retrieve process name/path for Admin-elevated applications
- Shows placeholder `Process_<PID>` (e.g., `Process_1234`)
- This is a Windows security feature, not a bug

**Workaround:**
- Some Admin tools can be run non-elevated (check application settings)
- If process runs elevated consistently, ensure service also runs elevated (use `LocalSystem` account)
- Track by window title when available

### No Usage Data Being Recorded

**Symptoms:** Service starts successfully but no usage sessions appear in database.

**Diagnosis:**
1. Verify service is running:
   ```powershell
   Get-Service -Name AppTimeTracker
   ```

2. Check for FocusChanged events in logs:
   ```powershell
   Get-EventLog -LogName Application -Source AppTimeTracker -EntryType Information -Newest 50 | grep "Focus changed"
   ```

3. Query database directly:
   ```powershell
   # Using SQLite CLI (if installed)
   sqlite3 "C:\ProgramData\AppTimeTracker\apptracker.db" "SELECT COUNT(*) FROM AppUsageSessions;"
   ```

**Common Causes & Solutions:**

| Cause | Solution |
|-------|----------|
| Service hasn't been running long | Service records sessions at 30-second intervals; let it run for a few minutes |
| `SessionMonitoring:Enabled` is false | Set to `true` in appsettings.json; service will skip tracking otherwise |
| Service is paused due to lock | Unlock computer; check `SessionMonitoring:PauseOnLock` setting |
| Focus events filtered or disabled | Verify registry: `HKEY_LOCAL_MACHINE\System\CurrentControlSet\Services\AppTimeTracker` exists |

### High CPU Usage

**Symptoms:** Service appears to consume excessive CPU.

**Diagnosis:**
1. Check Process monitor in Task Manager
2. Increase logging level to Debug:
   ```json
   "Serilog": {
     "MinimumLevel": { "Default": "Debug" }
   }
   ```

**Common Causes & Solutions:**

| Cause | Solution |
|-------|----------|
| Polling interval too short | Increase `Service:PollingIntervalMs` (default is reasonable at 500ms) |
| Too many focus events | Filter events in `FocusMonitorService` or increase `SessionFlushIntervalMs` |
| EF Core query performance | Rebuild indexes; use database analysis tools |

## Common Service Management Commands

**Start Service:**
```powershell
Start-Service -Name AppTimeTracker
```

**Stop Service:**
```powershell
Stop-Service -Name AppTimeTracker -Force
```

**Restart Service:**
```powershell
Restart-Service -Name AppTimeTracker
```

**Check Service Status:**
```powershell
Get-Service -Name AppTimeTracker | Select-Object Status, StartType
```

**View Service Properties:**
```powershell
Get-Service -Name AppTimeTracker | Select-Object *
```

**View Application Logs:**
```powershell
Get-EventLog -LogName Application -Source AppTimeTracker -Newest 50
```

**Set Service to Manual Startup:**
```powershell
Set-Service -Name AppTimeTracker -StartupType Manual
```

**Set Service to Automatic Startup:**
```powershell
Set-Service -Name AppTimeTracker -StartupType Automatic
```

## Development & Building

### Prerequisites for Development

- **.NET 8 SDK**: [Download .NET 8](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Visual Studio 2022** or **Visual Studio Code** with C# extension
- **Windows 10/11** for testing (requires Windows API for development)

### Build Commands

```bash
# Restore dependencies
dotnet restore

# Build debug version
dotnet build

# Build release version
dotnet build -c Release

# Publish for deployment
dotnet publish -c Release -r win-x64 --self-contained false

# Run unit tests (if available)
dotnet test
```

### Project Structure

```
├── src/
│   ├── Models/
│   │   └── AppUsageSession.cs          # Database entity model
│   ├── Services/
│   │   ├── FocusMonitorService.cs      # Focus change detection
│   │   ├── SessionMonitorService.cs    # Session state monitoring
│   │   ├── DatabaseInitializer.cs      # Database migration/setup
│   │   └── IFocusMonitorService.cs     # Service interfaces
│   ├── Native/
│   │   ├── FocusInterop.cs             # Win32 P/Invoke for focus
│   │   ├── SessionInterop.cs           # Win32 P/Invoke for sessions
│   │   └── MessageWindow.cs            # Hidden window for messages
│   ├── Data/
│   │   ├── AppDbContext.cs             # Entity Framework context
│   │   ├── UsageRepository.cs          # Data access layer
│   │   └── IUsageRepository.cs         # Repository interface
│   ├── Migrations/                     # EF Core migrations
│   ├── Program.cs                      # Service entry point
│   ├── Worker.cs                       # Main processing loop
│   ├── appsettings.json                # Configuration
│   └── AppTimeTracker.csproj           # Project file
├── scripts/
│   ├── Install-Service.ps1             # Installation script
│   └── Uninstall-Service.ps1           # Uninstallation script
└── README.md                            # This file
```

## Security Considerations

### Permissions Model

- **Installation**: Requires local administrator to register service
- **Execution**: Runs under configured service account (LocalSystem by default)
- **Database**: Service account owns database file with restricted ACLs
- **EventLog**: Requires registry permissions to create event sources

### Data Privacy

- **User ID**: Sessions are tagged with Windows user ID for multi-user support
- **Executable Paths**: Stored but can be cleared/anonymized if needed
- **Window Titles**: Captured for context but can be anonymized
- **Local Storage**: All data remains on local machine (no cloud sync)

### Elevation Limitation

- Non-elevated service cannot track Admin-elevated processes
- This is a Windows security boundary, not a configuration issue
- Solution: Run service as LocalSystem (default) which has higher privileges

## Support & Documentation

For additional information:

- **Event Viewer Logs**: `Event Viewer > Windows Logs > Application > Filter by Source: AppTimeTracker`
- **Service Logs**: `C:\ProgramData\AppTimeTracker\Logs\`
- **Database File**: `C:\ProgramData\AppTimeTracker\apptracker.db` (SQLite format)
- **Configuration**: `src/appsettings.json` in the installation directory

## License

[Your License Here]

## Contributing

Contributions are welcome! Please ensure:

1. Code follows C# conventions and includes XML documentation
2. P/Invoke declarations are properly documented with their Win32 origins
3. Database operations use transactions for consistency
4. Logging includes appropriate context for debugging

---

**Last Updated:** January 2025
**Version:** 1.0.0
**.NET Version:** 8.0
