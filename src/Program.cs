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

    // Register hosted services
    builder.Services.AddHostedService<DatabaseInitializer>();
    builder.Services.AddHostedService<Worker>();

    // Configure shutdown timeout for graceful shutdown
    builder.Services.Configure<HostOptions>(options =>
    {
        options.ShutdownTimeout = TimeSpan.FromSeconds(30);
    });

    var host = builder.Build();

    Log.Information("AppTimeTracker service configured successfully");

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
