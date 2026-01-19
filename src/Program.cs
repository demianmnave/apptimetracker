using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using AppTimeTracker;
using AppTimeTracker.Data;
using AppTimeTracker.Services;

// Configure Serilog early for startup logging
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting AppTimeTracker service...");

    var builder = Host.CreateApplicationBuilder(args);

    // Configure Windows Service
    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = "AppTimeTracker";
    });

    // Get database path from configuration
    var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
    var defaultDbPath = Path.Combine(programData, "AppTimeTracker", "apptracker.db");
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? $"Data Source={defaultDbPath}";

    // Configure Entity Framework Core with SQLite
    builder.Services.AddDbContext<AppDbContext>(options =>
    {
        options.UseSqlite(connectionString, sqliteOptions =>
        {
            sqliteOptions.MigrationsAssembly("AppTimeTracker");
        });
    });

    // Configure Serilog from configuration
    builder.Services.AddSerilog((services, lc) =>
    {
        var logPath = Path.Combine(programData, "AppTimeTracker", "Logs", "apptracker-.log");

        lc.ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                fileSizeLimitBytes: 10_000_000);

        // EventLog sink is only available on Windows
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            lc.WriteTo.EventLog(
                source: "AppTimeTracker",
                logName: "Application",
                restrictedToMinimumLevel: LogEventLevel.Warning,
                manageEventSource: false);
        }
    });

    // Register core service implementations
    builder.Services.AddSingleton<ISessionMonitorService, SessionMonitorService>();

    // Register hosted services (order matters: DatabaseInitializer first, then SessionMonitorService, then Worker)
    builder.Services.AddHostedService<DatabaseInitializer>();
    builder.Services.AddHostedService<SessionMonitorService>();
    builder.Services.AddHostedService<Worker>();

    // Configure shutdown timeout for graceful shutdown
    builder.Services.Configure<HostOptions>(options =>
    {
        var shutdownTimeout = builder.Configuration.GetValue("Service:GracefulShutdownTimeoutSeconds", 30);
        options.ShutdownTimeout = TimeSpan.FromSeconds(shutdownTimeout);
        Log.Information("Configured graceful shutdown timeout: {Timeout} seconds", shutdownTimeout);
    });

    // Create Windows Event Log source if running on Windows and not already registered
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        try
        {
            if (!EventLog.SourceExists("AppTimeTracker"))
            {
                EventLog.CreateEventSource("AppTimeTracker", "Application");
                Log.Information("Created Windows Event Log source: AppTimeTracker");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to create Event Log source (may require admin rights or already exists)");
        }
    }

    var host = builder.Build();

    // Log startup information with version
    var version = Assembly.GetExecutingAssembly().GetName().Version;
    Log.Information("AppTimeTracker service configured successfully. Version: {Version}, Started: {StartTime}",
        version ?? new Version(0, 0, 0, 0),
        DateTime.UtcNow);

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "AppTimeTracker service terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
