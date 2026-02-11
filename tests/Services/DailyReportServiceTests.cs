using NUnit.Framework;
using Microsoft.Extensions.Logging;
using Moq;
using AppTimeTracker.Data;
using AppTimeTracker.Models;
using AppTimeTracker.Services;

namespace AppTimeTracker.Tests.Services;

/// <summary>
/// Unit tests for DailyReportService.
/// Tests report generation, aggregation, validation, and edge cases.
/// </summary>
[TestFixture]
public class DailyReportServiceTests
{
    private Mock<IUsageRepository> _repositoryMock = null!;
    private Mock<ILogger<DailyReportService>> _loggerMock = null!;
    private IDailyReportService _service = null!;

    [SetUp]
    public void Setup()
    {
        _repositoryMock = new Mock<IUsageRepository>();
        _loggerMock = new Mock<ILogger<DailyReportService>>();
        _service = new DailyReportService(_loggerMock.Object, _repositoryMock.Object);
    }

    #region GenerateDailyReportAsync Tests

    [Test]
    public async Task GenerateDailyReportAsync_WithValidInput_ReturnsValidReport()
    {
        // Arrange
        var reportDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var userId = "user1";

        var sessions = new List<AppUsageSession>
        {
            new() { ProcessName = "chrome.exe", DurationSeconds = 300, SessionDate = reportDate, UserId = userId, StartTimeUtc = DateTime.UtcNow },
            new() { ProcessName = "notepad.exe", DurationSeconds = 200, SessionDate = reportDate, UserId = userId, StartTimeUtc = DateTime.UtcNow }
        };

        var aggregation = new Dictionary<string, long>
        {
            { "chrome.exe", 300 },
            { "notepad.exe", 200 }
        };

        _repositoryMock.Setup(r => r.GetSessionsByDateRangeAsync(reportDate, reportDate, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);
        _repositoryMock.Setup(r => r.GetAggregatedDurationByProcessAsync(reportDate, reportDate, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aggregation);

        // Act
        var result = await _service.GenerateDailyReportAsync(reportDate, userId, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.ReportDate, Is.EqualTo(reportDate));
        Assert.That(result.UserId, Is.EqualTo(userId));
        Assert.That(result.TotalUsageSeconds, Is.EqualTo(500));
        Assert.That(result.TotalSessions, Is.EqualTo(2));
    }

    [Test]
    public async Task GenerateDailyReportAsync_WithNoSessions_ReturnsEmptyReport()
    {
        // Arrange
        var reportDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var userId = "user1";

        _repositoryMock.Setup(r => r.GetSessionsByDateRangeAsync(reportDate, reportDate, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AppUsageSession>());
        _repositoryMock.Setup(r => r.GetAggregatedDurationByProcessAsync(reportDate, reportDate, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, long>());

        // Act
        var result = await _service.GenerateDailyReportAsync(reportDate, userId, CancellationToken.None);

        // Assert
        Assert.That(result.TotalUsageSeconds, Is.EqualTo(0));
        Assert.That(result.TotalSessions, Is.EqualTo(0));
        Assert.That(result.TopApplications, Is.Empty);
    }

    [Test]
    public async Task GenerateDailyReportAsync_CalculatesPercentageCorrectly()
    {
        // Arrange
        var reportDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var userId = "user1";

        var sessions = new List<AppUsageSession>
        {
            new() { ProcessName = "app1.exe", DurationSeconds = 600, SessionDate = reportDate, UserId = userId, StartTimeUtc = DateTime.UtcNow },
            new() { ProcessName = "app2.exe", DurationSeconds = 400, SessionDate = reportDate, UserId = userId, StartTimeUtc = DateTime.UtcNow }
        };

        var aggregation = new Dictionary<string, long>
        {
            { "app1.exe", 600 },
            { "app2.exe", 400 }
        };

        _repositoryMock.Setup(r => r.GetSessionsByDateRangeAsync(reportDate, reportDate, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);
        _repositoryMock.Setup(r => r.GetAggregatedDurationByProcessAsync(reportDate, reportDate, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aggregation);

        // Act
        var result = await _service.GenerateDailyReportAsync(reportDate, userId, CancellationToken.None);

        // Assert
        var app1 = result.TopApplications.First(a => a.ProcessName == "app1.exe");
        var app2 = result.TopApplications.First(a => a.ProcessName == "app2.exe");

        Assert.That(app1.PercentageOfTotal, Is.EqualTo(60m)); // 600/1000 = 60%
        Assert.That(app2.PercentageOfTotal, Is.EqualTo(40m)); // 400/1000 = 40%
    }

    [Test]
    public async Task GenerateDailyReportAsync_SortsApplicationsByUsageDescending()
    {
        // Arrange
        var reportDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var userId = "user1";

        var sessions = new List<AppUsageSession>
        {
            new() { ProcessName = "chrome.exe", DurationSeconds = 100, SessionDate = reportDate, UserId = userId, StartTimeUtc = DateTime.UtcNow },
            new() { ProcessName = "vscode.exe", DurationSeconds = 500, SessionDate = reportDate, UserId = userId, StartTimeUtc = DateTime.UtcNow },
            new() { ProcessName = "notepad.exe", DurationSeconds = 200, SessionDate = reportDate, UserId = userId, StartTimeUtc = DateTime.UtcNow }
        };

        var aggregation = new Dictionary<string, long>
        {
            { "chrome.exe", 100 },
            { "vscode.exe", 500 },
            { "notepad.exe", 200 }
        };

        _repositoryMock.Setup(r => r.GetSessionsByDateRangeAsync(reportDate, reportDate, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);
        _repositoryMock.Setup(r => r.GetAggregatedDurationByProcessAsync(reportDate, reportDate, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aggregation);

        // Act
        var result = await _service.GenerateDailyReportAsync(reportDate, userId, CancellationToken.None);

        // Assert
        Assert.That(result.TopApplications[0].ProcessName, Is.EqualTo("vscode.exe")); // 500
        Assert.That(result.TopApplications[1].ProcessName, Is.EqualTo("notepad.exe")); // 200
        Assert.That(result.TopApplications[2].ProcessName, Is.EqualTo("chrome.exe")); // 100
    }

    [Test]
    public async Task GenerateDailyReportAsync_CalculatesAverageSessionDuration()
    {
        // Arrange
        var reportDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var userId = "user1";

        var sessions = new List<AppUsageSession>
        {
            new() { ProcessName = "app.exe", DurationSeconds = 100, SessionDate = reportDate, UserId = userId, StartTimeUtc = DateTime.UtcNow },
            new() { ProcessName = "app.exe", DurationSeconds = 200, SessionDate = reportDate, UserId = userId, StartTimeUtc = DateTime.UtcNow },
            new() { ProcessName = "app.exe", DurationSeconds = 300, SessionDate = reportDate, UserId = userId, StartTimeUtc = DateTime.UtcNow }
        };

        var aggregation = new Dictionary<string, long> { { "app.exe", 600 } };

        _repositoryMock.Setup(r => r.GetSessionsByDateRangeAsync(reportDate, reportDate, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);
        _repositoryMock.Setup(r => r.GetAggregatedDurationByProcessAsync(reportDate, reportDate, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aggregation);

        // Act
        var result = await _service.GenerateDailyReportAsync(reportDate, userId, CancellationToken.None);

        // Assert
        Assert.That(result.AverageSessionDurationSeconds, Is.EqualTo(200)); // 600 / 3
    }

    #endregion

    #region GenerateDateRangeReportAsync Tests

    [Test]
    public async Task GenerateDateRangeReportAsync_WithDateRange_ReturnsReportForEachDay()
    {
        // Arrange
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var userId = "user1";

        var sessions = new List<AppUsageSession>();
        var aggregation = new Dictionary<string, long>();

        _repositoryMock.Setup(r => r.GetSessionsByDateRangeAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);
        _repositoryMock.Setup(r => r.GetAggregatedDurationByProcessAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aggregation);

        // Act
        var result = await _service.GenerateDateRangeReportAsync(startDate, endDate, userId, CancellationToken.None);

        // Assert
        Assert.That(result, Has.Count.EqualTo(3)); // 3 days
    }

    [Test]
    public async Task GenerateDateRangeReportAsync_WithSingleDay_ReturnsSingleReport()
    {
        // Arrange
        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        var userId = "user1";

        var sessions = new List<AppUsageSession>();
        var aggregation = new Dictionary<string, long>();

        _repositoryMock.Setup(r => r.GetSessionsByDateRangeAsync(date, date, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);
        _repositoryMock.Setup(r => r.GetAggregatedDurationByProcessAsync(date, date, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aggregation);

        // Act
        var result = await _service.GenerateDateRangeReportAsync(date, date, userId, CancellationToken.None);

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].ReportDate, Is.EqualTo(date));
    }

    #endregion

    #region GetTopApplicationsAsync Tests

    [Test]
    public async Task GetTopApplicationsAsync_WithValidInput_ReturnsTopApplications()
    {
        // Arrange
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var userId = "user1";

        var sessions = new List<AppUsageSession>
        {
            new() { ProcessName = "app1.exe", DurationSeconds = 100, SessionDate = startDate, UserId = userId, StartTimeUtc = DateTime.UtcNow },
            new() { ProcessName = "app2.exe", DurationSeconds = 200, SessionDate = startDate, UserId = userId, StartTimeUtc = DateTime.UtcNow },
            new() { ProcessName = "app3.exe", DurationSeconds = 300, SessionDate = startDate, UserId = userId, StartTimeUtc = DateTime.UtcNow }
        };

        var aggregation = new Dictionary<string, long>
        {
            { "app1.exe", 100 },
            { "app2.exe", 200 },
            { "app3.exe", 300 }
        };

        _repositoryMock.Setup(r => r.GetSessionsByDateRangeAsync(startDate, endDate, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);
        _repositoryMock.Setup(r => r.GetAggregatedDurationByProcessAsync(startDate, endDate, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aggregation);

        // Act
        var result = await _service.GetTopApplicationsAsync(startDate, endDate, userId, topCount: 2, CancellationToken.None);

        // Assert
        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[0].ProcessName, Is.EqualTo("app3.exe")); // 300
        Assert.That(result[1].ProcessName, Is.EqualTo("app2.exe")); // 200
    }

    [Test]
    public async Task GetTopApplicationsAsync_WithTopCountLargerThanResults_ReturnsAllApplications()
    {
        // Arrange
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var userId = "user1";

        var sessions = new List<AppUsageSession>
        {
            new() { ProcessName = "app1.exe", DurationSeconds = 100, SessionDate = startDate, UserId = userId, StartTimeUtc = DateTime.UtcNow },
            new() { ProcessName = "app2.exe", DurationSeconds = 200, SessionDate = startDate, UserId = userId, StartTimeUtc = DateTime.UtcNow }
        };

        var aggregation = new Dictionary<string, long>
        {
            { "app1.exe", 100 },
            { "app2.exe", 200 }
        };

        _repositoryMock.Setup(r => r.GetSessionsByDateRangeAsync(startDate, endDate, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);
        _repositoryMock.Setup(r => r.GetAggregatedDurationByProcessAsync(startDate, endDate, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aggregation);

        // Act
        var result = await _service.GetTopApplicationsAsync(startDate, endDate, userId, topCount: 100, CancellationToken.None);

        // Assert
        Assert.That(result, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task GetTopApplicationsAsync_WithTopCountZero_ThrowsArgumentException()
    {
        // Arrange
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var userId = "user1";

        // Act & Assert
        var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
            await _service.GetTopApplicationsAsync(startDate, endDate, userId, topCount: 0, CancellationToken.None));

        Assert.That(ex!.Message, Does.Contain("greater than 0"));
    }

    [Test]
    public async Task GetTopApplicationsAsync_WithTopCountNegative_ThrowsArgumentException()
    {
        // Arrange
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var userId = "user1";

        // Act & Assert
        var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
            await _service.GetTopApplicationsAsync(startDate, endDate, userId, topCount: -1, CancellationToken.None));

        Assert.That(ex!.Message, Does.Contain("greater than 0"));
    }

    [Test]
    public async Task GetTopApplicationsAsync_WithDefaultTopCount_Returns10Applications()
    {
        // Arrange
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var userId = "user1";

        var sessions = Enumerable.Range(1, 15)
            .Select(i => new AppUsageSession
            {
                ProcessName = $"app{i}.exe",
                DurationSeconds = i * 100,
                SessionDate = startDate,
                UserId = userId,
                StartTimeUtc = DateTime.UtcNow
            })
            .ToList();

        var aggregation = sessions
            .GroupBy(s => s.ProcessName)
            .ToDictionary(g => g.Key, g => (long)g.Sum(s => s.DurationSeconds));

        _repositoryMock.Setup(r => r.GetSessionsByDateRangeAsync(startDate, endDate, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);
        _repositoryMock.Setup(r => r.GetAggregatedDurationByProcessAsync(startDate, endDate, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aggregation);

        // Act
        var result = await _service.GetTopApplicationsAsync(startDate, endDate, userId, CancellationToken.None);

        // Assert - Default topCount is 10
        Assert.That(result, Has.Count.EqualTo(10));
    }

    #endregion

    #region Formatting Tests

    [Test]
    public async Task GenerateDailyReportAsync_FormattedTotalUsage_UsesDurationFormatter()
    {
        // Arrange
        var reportDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var userId = "user1";

        var sessions = new List<AppUsageSession>
        {
            new() { ProcessName = "app.exe", DurationSeconds = 3725, SessionDate = reportDate, UserId = userId, StartTimeUtc = DateTime.UtcNow }
        };

        var aggregation = new Dictionary<string, long> { { "app.exe", 3725 } };

        _repositoryMock.Setup(r => r.GetSessionsByDateRangeAsync(reportDate, reportDate, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);
        _repositoryMock.Setup(r => r.GetAggregatedDurationByProcessAsync(reportDate, reportDate, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aggregation);

        // Act
        var result = await _service.GenerateDailyReportAsync(reportDate, userId, CancellationToken.None);

        // Assert
        Assert.That(result.FormattedTotalUsage, Is.EqualTo("1h 2m 5s"));
    }

    [Test]
    public async Task GenerateDailyReportAsync_FormattedApplicationDuration_UsesDurationFormatter()
    {
        // Arrange
        var reportDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var userId = "user1";

        var sessions = new List<AppUsageSession>
        {
            new() { ProcessName = "app.exe", DurationSeconds = 305, SessionDate = reportDate, UserId = userId, StartTimeUtc = DateTime.UtcNow }
        };

        var aggregation = new Dictionary<string, long> { { "app.exe", 305 } };

        _repositoryMock.Setup(r => r.GetSessionsByDateRangeAsync(reportDate, reportDate, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);
        _repositoryMock.Setup(r => r.GetAggregatedDurationByProcessAsync(reportDate, reportDate, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aggregation);

        // Act
        var result = await _service.GenerateDailyReportAsync(reportDate, userId, CancellationToken.None);

        // Assert
        var appUsage = result.TopApplications[0];
        Assert.That(appUsage.FormattedDuration, Is.EqualTo("5m 5s"));
    }

    #endregion

    #region Exception Handling Tests

    [Test]
    public void GenerateDailyReportAsync_WithRepositoryException_PropagatesException()
    {
        // Arrange
        var reportDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var userId = "user1";

        _repositoryMock.Setup(r => r.GetSessionsByDateRangeAsync(reportDate, reportDate, userId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _service.GenerateDailyReportAsync(reportDate, userId, CancellationToken.None));
    }

    #endregion

    #region Logging Tests

    [Test]
    public async Task GenerateDailyReportAsync_LogsReportGeneration()
    {
        // Arrange
        var reportDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var userId = "user1";

        var sessions = new List<AppUsageSession>();
        var aggregation = new Dictionary<string, long>();

        _repositoryMock.Setup(r => r.GetSessionsByDateRangeAsync(reportDate, reportDate, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);
        _repositoryMock.Setup(r => r.GetAggregatedDurationByProcessAsync(reportDate, reportDate, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aggregation);

        // Act
        await _service.GenerateDailyReportAsync(reportDate, userId, CancellationToken.None);

        // Assert
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((msg, _) => msg.ToString()!.Contains("Generating daily report")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion
}
