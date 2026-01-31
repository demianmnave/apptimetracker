# AppTimeTracker

A Windows service that tracks application usage by monitoring which windows have focus, aggregating session data, and storing it in a local SQLite database.

## Tech Stack

- .NET 8 Worker Service (Windows Service)
- C# 12
- Entity Framework Core 8 with SQLite
- P/Invoke (user32.dll) for Windows API
- Serilog with Windows Event Log sink

## Project Structure

```
src/
├── Program.cs              # Entry point, service host configuration
├── Worker.cs               # Main background service loop
├── Services/
│   ├── FocusDetection/     # Windows API calls to detect active window
│   ├── ProcessResolver/    # Resolves process info from window handles
│   └── SessionAggregator/  # Aggregates focus time into sessions
├── Data/
│   ├── AppDbContext.cs     # EF Core context
│   └── UsageRepository.cs  # Data access for usage sessions
└── Models/
    └── AppUsageSession.cs  # Session entity model
```

## Key Components

| Component | Location | Purpose |
|-----------|----------|---------|
| Focus Detection | `src/Services/FocusDetection/` | P/Invoke calls to user32.dll for active window |
| Process Resolver | `src/Services/ProcessResolver/` | Gets app name/path from process ID |
| Session Aggregator | `src/Services/SessionAggregator/` | Tracks duration, creates session records |
| Usage Repository | `src/Data/UsageRepository.cs` | CRUD operations for AppUsageSessions |
| Database | SQLite file (local) | Stores `AppUsageSessions` table |

## How to Work on This Project

### Build & Run
```bash
dotnet build
dotnet run
```

### Run as Windows Service
```bash
dotnet publish -c Release
sc create AppTimeTracker binPath="path\to\AppTimeTracker.exe"
sc start AppTimeTracker
```

### Run Tests
```bash
dotnet test
```

### Database Migrations
```bash
dotnet ef migrations add <MigrationName>
dotnet ef database update
```

## Architecture Notes

- The Worker runs on a polling loop detecting the foreground window
- Focus changes trigger session end/start events in the aggregator
- Sessions are persisted via EF Core to SQLite
- Logging goes to Windows Event Log via Serilog
- See `Worker.cs` for the main service loop logic

## Conventions

- Use dependency injection for all services
- P/Invoke declarations should stay isolated in FocusDetection
- All database access goes through the repository pattern
- Log significant events (app switches, errors) to Event Log
- Handle graceful shutdown via cancellation tokens