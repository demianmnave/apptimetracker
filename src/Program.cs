using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using AppTimeTracker;
using AppTimeTracker.Configuration;
using AppTimeTracker.Data;
using AppTimeTracker.Services;
using AppTimeTracker.Services.HealthMonitors;

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

    // Bind configuration sections
    builder.Services.Configure<LoggingSettings>(builder.Configuration.GetSection("LoggingSettings"));
    builder.Services.Configure<HealthCheckSettings>(builder.Configuration.GetSection("HealthCheckSettings"));

    // Configure Serilog from configuration with enhanced file rotation and structured enrichers
    builder.Services.AddSerilog((services, lc) =>
    {
        var logPath = Path.Combine(programData, "AppTimeTracker", "Logs", "apptracker-.log");
        var loggingSettings = services.GetRequiredService<IOptions<LoggingSettings>>().Value;

        lc.ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithThreadId()
            .Enrich.WithProcessId()
            .Enrich.WithProperty("Environment", "Production")
            .WriteTo.Console()
            .WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Day,
                fileSizeLimitBytes: loggingSettings.MaxFileSizeBytes,
                retainedFileCountLimit: loggingSettings.RetainedFileCount,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] [PID:{ProcessId} TID:{ThreadId}] {Message:lj}{NewLine}{Exception}");

        // EventLog sink is only available on Windows
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && loggingSettings.EventLogFallbackEnabled)
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
    builder.Services.AddSingleton<IFocusMonitorService, FocusMonitorService>();

    // Register data repository
    builder.Services.AddScoped<IUsageRepository, UsageRepository>();

    // Register logging and health services
    builder.Services.AddSingleton<ILoggingService, LoggingService>();
    builder.Services.AddSingleton<IRecoveryManager, RecoveryManager>();

    // Register health monitors
    builder.Services.AddSingleton<SqliteHealthMonitor>();
    builder.Services.AddSingleton<WinEventHookHealthMonitor>();
    builder.Services.AddSingleton<MemoryHealthMonitor>();

    // Register hosted services (order matters: DatabaseInitializer first, then SessionMonitorService, then FocusMonitorService, then HealthCheckService, then Worker)
    builder.Services.AddHostedService<DatabaseInitializer>();
    builder.Services.AddHostedService<SessionMonitorService>();
    builder.Services.AddHostedService<FocusMonitorService>();
    builder.Services.AddHostedService<HealthCheckService>();
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
