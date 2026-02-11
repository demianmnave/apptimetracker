using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using AppTimeTracker.Data;

namespace AppTimeTracker.Services;

/// <summary>
/// Hosted service that initializes the database on startup.
/// Handles database creation, migration, and corruption recovery.
/// </summary>
public class DatabaseInitializer : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DatabaseInitializer> _logger;
    private readonly IConfiguration _configuration;

    public DatabaseInitializer(
        IServiceProvider serviceProvider,
        ILogger<DatabaseInitializer> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Database initialization starting...");

        try
        {
            // Ensure ProgramData directory exists
            var dbPath = GetDatabasePath();
            var dbDirectory = Path.GetDirectoryName(dbPath);

            if (!string.IsNullOrEmpty(dbDirectory) && !Directory.Exists(dbDirectory))
            {
                Directory.CreateDirectory(dbDirectory);
                _logger.LogInformation("Created database directory: {Directory}", dbDirectory);
            }

            // Check database integrity if it exists
            if (File.Exists(dbPath))
            {
                var isValid = await CheckDatabaseIntegrityAsync(dbPath, cancellationToken);
                if (!isValid)
                {
                    await HandleCorruptDatabaseAsync(dbPath, cancellationToken);
                }
            }

            // Apply migrations
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            await dbContext.Database.MigrateAsync(cancellationToken);
            _logger.LogInformation("Database migrations applied successfully");

            // Verify final state
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
            if (canConnect)
            {
                _logger.LogInformation("Database initialization completed. Path: {Path}", dbPath);
            }
            else
            {
                _logger.LogError("Database connection verification failed");
                throw new InvalidOperationException("Failed to verify database connection after initialization");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database initialization failed");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Database initializer stopping...");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the database file path from configuration.
    /// </summary>
    private string GetDatabasePath()
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrEmpty(connectionString))
        {
            // Default to ProgramData location
            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            return Path.Combine(programData, "AppTimeTracker", "apptracker.db");
        }

        // Extract path from connection string
        var builder = new SqliteConnectionStringBuilder(connectionString);
        return builder.DataSource;
    }

    /// <summary>
    /// Checks database integrity using SQLite PRAGMA integrity_check.
    /// </summary>
    private async Task<bool> CheckDatabaseIntegrityAsync(string dbPath, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Checking database integrity...");

        try
        {
            var connectionString = $"Data Source={dbPath}";
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA integrity_check;";

            var result = await command.ExecuteScalarAsync(cancellationToken);
            var isValid = result?.ToString()?.Equals("ok", StringComparison.OrdinalIgnoreCase) ?? false;

            if (isValid)
            {
                _logger.LogInformation("Database integrity check passed");
            }
            else
            {
                _logger.LogWarning("Database integrity check failed: {Result}", result);
            }

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Database integrity check encountered an error");
            return false;
        }
    }

    /// <summary>
    /// Handles corrupt database by backing up and recreating.
    /// </summary>
    private async Task HandleCorruptDatabaseAsync(string dbPath, CancellationToken cancellationToken)
    {
        _logger.LogWarning("Handling corrupt database at: {Path}", dbPath);

        try
        {
            // Create backup with timestamp
            var backupPath = $"{dbPath}.corrupt.{DateTime.UtcNow:yyyyMMddHHmmss}";
            File.Move(dbPath, backupPath);
            _logger.LogInformation("Corrupt database backed up to: {BackupPath}", backupPath);

            // Also check for and remove -wal and -shm files
            var walPath = $"{dbPath}-wal";
            var shmPath = $"{dbPath}-shm";

            if (File.Exists(walPath))
            {
                File.Move(walPath, $"{walPath}.corrupt.{DateTime.UtcNow:yyyyMMddHHmmss}");
            }

            if (File.Exists(shmPath))
            {
                File.Move(shmPath, $"{shmPath}.corrupt.{DateTime.UtcNow:yyyyMMddHHmmss}");
            }

            _logger.LogInformation("Corrupt database files backed up. A new database will be created.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle corrupt database");
            throw;
        }

        await Task.CompletedTask;
    }
}
