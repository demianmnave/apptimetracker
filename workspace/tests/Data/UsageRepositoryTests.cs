using NUnit.Framework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using AppTimeTracker.Data;
using AppTimeTracker.Models;

namespace AppTimeTracker.Tests.Data;

/// <summary>
/// Unit tests for UsageRepository.
/// Tests CRUD operations, query optimization, and aggregation logic.
/// </summary>
[TestFixture]
public class UsageRepositoryTests
{
    private AppDbContext _context = null!;
    private IUsageRepository _repository = null!;
    private Mock<ILogger<UsageRepository>> _loggerMock = null!;

    [SetUp]
    public void Setup()
    {
        // Create in-memory database for testing
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _loggerMock = new Mock<ILogger<UsageRepository>>();
        _repository = new UsageRepository(_context, _loggerMock.Object);
    }

    [TearDown]
    public void Cleanup()
    {
        _context?.Dispose();
    }

    #region SaveSessionAsync Tests

    [Test]
    public async Task SaveSessionAsync_WithValidSession_SavesSuccessfully()
    {
        // Arrange
        var session = new AppUsageSession
        {
            ProcessName = "notepad.exe",
            ExecutablePath = "C:\\Windows\\notepad.exe",
            WindowTitle = "Untitled - Notepad",
            StartTimeUtc = DateTime.UtcNow,
            EndTimeUtc = DateTime.UtcNow.AddMinutes(5),
            SessionDate = DateOnly.FromDateTime(DateTime.UtcNow),
            UserId = "user1",
            DurationSeconds = 300
        };

        // Act
        var result = await _repository.SaveSessionAsync(session, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Id, Is.GreaterThan(0));
        Assert.That(result.ProcessName, Is.EqualTo("notepad.exe"));
    }

    [Test]
    public async Task SaveSessionAsync_WithNullSession_ThrowsException()
    {
        // Arrange
        AppUsageSession? session = null;

        // Act & Assert
        Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _repository.SaveSessionAsync(session!, CancellationToken.None));
    }

    [Test]
    public async Task SaveSessionAsync_PersistsToDatabase()
    {
        // Arrange
        var session = new AppUsageSession
        {
            ProcessName = "chrome.exe",
            ExecutablePath = "C:\\Program Files\\Google\\Chrome\\chrome.exe",
            WindowTitle = "Google Chrome",
            StartTimeUtc = DateTime.UtcNow,
            EndTimeUtc = DateTime.UtcNow.AddMinutes(10),
            SessionDate = DateOnly.FromDateTime(DateTime.UtcNow),
            UserId = "user1",
            DurationSeconds = 600
        };

        // Act
        await _repository.SaveSessionAsync(session, CancellationToken.None);

        // Assert - Query database directly
        var savedSession = await _context.AppUsageSessions.FirstOrDefaultAsync(s => s.ProcessName == "chrome.exe");
        Assert.That(savedSession, Is.Not.Null);
        Assert.That(savedSession!.DurationSeconds, Is.EqualTo(600));
    }

    [Test]
    public async Task SaveSessionAsync_MultipleInserts_AllSucceed()
    {
        // Arrange
        var sessions = Enumerable.Range(1, 5)
            .Select(i => new AppUsageSession
            {
                ProcessName = $"app{i}.exe",
                ExecutablePath = $"C:\\Apps\\app{i}.exe",
                WindowTitle = $"Application {i}",
                StartTimeUtc = DateTime.UtcNow.AddHours(-i),
                EndTimeUtc = DateTime.UtcNow.AddHours(-i).AddMinutes(30),
                SessionDate = DateOnly.FromDateTime(DateTime.UtcNow),
                UserId = "user1",
                DurationSeconds = 1800
            })
            .ToList();

        // Act
        foreach (var session in sessions)
        {
            await _repository.SaveSessionAsync(session, CancellationToken.None);
        }

        // Assert
        var count = await _context.AppUsageSessions.CountAsync();
        Assert.That(count, Is.EqualTo(5));
    }

    #endregion

    #region UpdateSessionAsync Tests

    [Test]
    public async Task UpdateSessionAsync_WithExistingSession_UpdatesSuccessfully()
    {
        // Arrange
        var session = new AppUsageSession
        {
            ProcessName = "excel.exe",
            ExecutablePath = "C:\\Program Files\\Microsoft Office\\excel.exe",
            WindowTitle = "Book1 - Excel",
            StartTimeUtc = DateTime.UtcNow,
            EndTimeUtc = DateTime.UtcNow.AddMinutes(15),
            SessionDate = DateOnly.FromDateTime(DateTime.UtcNow),
            UserId = "user1",
            DurationSeconds = 900
        };

        // Save initial session
        var savedSession = await _repository.SaveSessionAsync(session, CancellationToken.None);

        // Update the session
        savedSession.DurationSeconds = 1200;
        savedSession.WindowTitle = "Updated - Excel";

        // Act
        var updatedSession = await _repository.UpdateSessionAsync(savedSession, CancellationToken.None);

        // Assert
        Assert.That(updatedSession.DurationSeconds, Is.EqualTo(1200));
        Assert.That(updatedSession.WindowTitle, Is.EqualTo("Updated - Excel"));
    }

    [Test]
    public async Task UpdateSessionAsync_PersistsChanges()
    {
        // Arrange
        var session = new AppUsageSession
        {
            ProcessName = "vscode.exe",
            ExecutablePath = "C:\\Users\\user\\AppData\\Local\\Programs\\Microsoft VS Code\\Code.exe",
            WindowTitle = "Visual Studio Code",
            StartTimeUtc = DateTime.UtcNow,
            EndTimeUtc = DateTime.UtcNow.AddHours(1),
            SessionDate = DateOnly.FromDateTime(DateTime.UtcNow),
            UserId = "user1",
            DurationSeconds = 3600
        };

        var savedSession = await _repository.SaveSessionAsync(session, CancellationToken.None);
        savedSession.DurationSeconds = 5400;

        // Act
        await _repository.UpdateSessionAsync(savedSession, CancellationToken.None);

        // Assert - Query fresh from database
        var dbSession = await _context.AppUsageSessions.FirstOrDefaultAsync(s => s.Id == savedSession.Id);
        Assert.That(dbSession!.DurationSeconds, Is.EqualTo(5400));
    }

    #endregion

    #region GetSessionsByDateRangeAsync Tests

    [Test]
    public async Task GetSessionsByDateRangeAsync_WithNoFilters_ReturnsAllSessions()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var sessions = new[]
        {
            new AppUsageSession { ProcessName = "app1.exe", SessionDate = today, UserId = "user1", DurationSeconds = 100, StartTimeUtc = DateTime.UtcNow },
            new AppUsageSession { ProcessName = "app2.exe", SessionDate = today, UserId = "user1", DurationSeconds = 200, StartTimeUtc = DateTime.UtcNow },
            new AppUsageSession { ProcessName = "app3.exe", SessionDate = today, UserId = "user1", DurationSeconds = 300, StartTimeUtc = DateTime.UtcNow }
        };

        foreach (var session in sessions)
        {
            await _repository.SaveSessionAsync(session, CancellationToken.None);
        }

        // Act
        var result = await _repository.GetSessionsByDateRangeAsync(null, null, null, CancellationToken.None);

        // Assert
        Assert.That(result, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task GetSessionsByDateRangeAsync_WithDateFilter_ReturnsMatchingEntries()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var yesterday = today.AddDays(-1);

        var todaySession = new AppUsageSession
        {
            ProcessName = "today.exe",
            SessionDate = today,
            UserId = "user1",
            DurationSeconds = 100,
            StartTimeUtc = DateTime.UtcNow
        };

        var yesterdaySession = new AppUsageSession
        {
            ProcessName = "yesterday.exe",
            SessionDate = yesterday,
            UserId = "user1",
            DurationSeconds = 200,
            StartTimeUtc = DateTime.UtcNow.AddDays(-1)
        };

        await _repository.SaveSessionAsync(todaySession, CancellationToken.None);
        await _repository.SaveSessionAsync(yesterdaySession, CancellationToken.None);

        // Act
        var result = await _repository.GetSessionsByDateRangeAsync(today, today, null, CancellationToken.None);

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].ProcessName, Is.EqualTo("today.exe"));
    }

    [Test]
    public async Task GetSessionsByDateRangeAsync_WithUserFilter_ReturnsUserSessions()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var user1Session = new AppUsageSession
        {
            ProcessName = "user1_app.exe",
            SessionDate = today,
            UserId = "user1",
            DurationSeconds = 100,
            StartTimeUtc = DateTime.UtcNow
        };

        var user2Session = new AppUsageSession
        {
            ProcessName = "user2_app.exe",
            SessionDate = today,
            UserId = "user2",
            DurationSeconds = 200,
            StartTimeUtc = DateTime.UtcNow
        };

        await _repository.SaveSessionAsync(user1Session, CancellationToken.None);
        await _repository.SaveSessionAsync(user2Session, CancellationToken.None);

        // Act
        var result = await _repository.GetSessionsByDateRangeAsync(null, null, "user1", CancellationToken.None);

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].UserId, Is.EqualTo("user1"));
    }

    [Test]
    public async Task GetSessionsByDateRangeAsync_ReturnsOrderedByStartTimeDescending()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var now = DateTime.UtcNow;

        var session1 = new AppUsageSession { ProcessName = "app1.exe", SessionDate = today, UserId = "user1", DurationSeconds = 100, StartTimeUtc = now.AddHours(-2) };
        var session2 = new AppUsageSession { ProcessName = "app2.exe", SessionDate = today, UserId = "user1", DurationSeconds = 100, StartTimeUtc = now };
        var session3 = new AppUsageSession { ProcessName = "app3.exe", SessionDate = today, UserId = "user1", DurationSeconds = 100, StartTimeUtc = now.AddHours(-1) };

        await _repository.SaveSessionAsync(session1, CancellationToken.None);
        await _repository.SaveSessionAsync(session2, CancellationToken.None);
        await _repository.SaveSessionAsync(session3, CancellationToken.None);

        // Act
        var result = await _repository.GetSessionsByDateRangeAsync(today, today, "user1", CancellationToken.None);

        // Assert
        Assert.That(result[0].ProcessName, Is.EqualTo("app2.exe")); // Most recent
        Assert.That(result[1].ProcessName, Is.EqualTo("app3.exe")); // Middle
        Assert.That(result[2].ProcessName, Is.EqualTo("app1.exe")); // Oldest
    }

    #endregion

    #region GetAggregatedDurationByProcessAsync Tests

    [Test]
    public async Task GetAggregatedDurationByProcessAsync_WithMultipleProcesses_AggregatesCorrectly()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var sessions = new[]
        {
            new AppUsageSession { ProcessName = "chrome.exe", SessionDate = today, UserId = "user1", DurationSeconds = 300, StartTimeUtc = DateTime.UtcNow },
            new AppUsageSession { ProcessName = "chrome.exe", SessionDate = today, UserId = "user1", DurationSeconds = 200, StartTimeUtc = DateTime.UtcNow },
            new AppUsageSession { ProcessName = "notepad.exe", SessionDate = today, UserId = "user1", DurationSeconds = 100, StartTimeUtc = DateTime.UtcNow },
            new AppUsageSession { ProcessName = "notepad.exe", SessionDate = today, UserId = "user1", DurationSeconds = 150, StartTimeUtc = DateTime.UtcNow }
        };

        foreach (var session in sessions)
        {
            await _repository.SaveSessionAsync(session, CancellationToken.None);
        }

        // Act
        var result = await _repository.GetAggregatedDurationByProcessAsync(today, today, "user1", CancellationToken.None);

        // Assert
        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result["chrome.exe"], Is.EqualTo(500)); // 300 + 200
        Assert.That(result["notepad.exe"], Is.EqualTo(250)); // 100 + 150
    }

    [Test]
    public async Task GetAggregatedDurationByProcessAsync_WithDateRange_AggregatesCorrectly()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var yesterday = today.AddDays(-1);

        var todaySession = new AppUsageSession { ProcessName = "app.exe", SessionDate = today, UserId = "user1", DurationSeconds = 300, StartTimeUtc = DateTime.UtcNow };
        var yesterdaySession = new AppUsageSession { ProcessName = "app.exe", SessionDate = yesterday, UserId = "user1", DurationSeconds = 200, StartTimeUtc = DateTime.UtcNow.AddDays(-1) };

        await _repository.SaveSessionAsync(todaySession, CancellationToken.None);
        await _repository.SaveSessionAsync(yesterdaySession, CancellationToken.None);

        // Act
        var result = await _repository.GetAggregatedDurationByProcessAsync(yesterday, today, "user1", CancellationToken.None);

        // Assert
        Assert.That(result["app.exe"], Is.EqualTo(500)); // 300 + 200 (both days)
    }

    [Test]
    public async Task GetAggregatedDurationByProcessAsync_WithUserFilter_AggregatesOnlyUserSessions()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var user1Session = new AppUsageSession { ProcessName = "app.exe", SessionDate = today, UserId = "user1", DurationSeconds = 300, StartTimeUtc = DateTime.UtcNow };
        var user2Session = new AppUsageSession { ProcessName = "app.exe", SessionDate = today, UserId = "user2", DurationSeconds = 200, StartTimeUtc = DateTime.UtcNow };

        await _repository.SaveSessionAsync(user1Session, CancellationToken.None);
        await _repository.SaveSessionAsync(user2Session, CancellationToken.None);

        // Act
        var result = await _repository.GetAggregatedDurationByProcessAsync(today, today, "user1", CancellationToken.None);

        // Assert
        Assert.That(result["app.exe"], Is.EqualTo(300)); // Only user1's session
    }

    [Test]
    public async Task GetAggregatedDurationByProcessAsync_WithNoMatches_ReturnsEmptyDictionary()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Act
        var result = await _repository.GetAggregatedDurationByProcessAsync(today, today, "nonexistent_user", CancellationToken.None);

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetAggregatedDurationByProcessAsync_WithSingleSession_ReturnsSingleEntry()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var session = new AppUsageSession
        {
            ProcessName = "single.exe",
            SessionDate = today,
            UserId = "user1",
            DurationSeconds = 500,
            StartTimeUtc = DateTime.UtcNow
        };

        await _repository.SaveSessionAsync(session, CancellationToken.None);

        // Act
        var result = await _repository.GetAggregatedDurationByProcessAsync(today, today, "user1", CancellationToken.None);

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result["single.exe"], Is.EqualTo(500));
    }

    #endregion

    #region CancellationToken Tests

    [Test]
    public async Task SaveSessionAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var session = new AppUsageSession { ProcessName = "app.exe", SessionDate = DateOnly.FromDateTime(DateTime.UtcNow), UserId = "user1", DurationSeconds = 100, StartTimeUtc = DateTime.UtcNow };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await _repository.SaveSessionAsync(session, cts.Token));
    }

    #endregion
}
