using NUnit.Framework;
using AppTimeTracker.Services;

namespace AppTimeTracker.Tests.Services;

/// <summary>
/// Unit tests for DurationFormatter utility class.
/// Tests formatting logic with various edge cases and inputs.
/// </summary>
[TestFixture]
public class DurationFormatterTests
{
    #region Basic Formatting Tests

    [Test]
    public void Format_WithZeroSeconds_ReturnsZeroSeconds()
    {
        // Arrange
        const long seconds = 0;

        // Act
        var result = DurationFormatter.Format(seconds);

        // Assert
        Assert.That(result, Is.EqualTo("0s"));
    }

    [Test]
    public void Format_WithSingleSecond_ReturnsSingleSecond()
    {
        // Arrange
        const long seconds = 1;

        // Act
        var result = DurationFormatter.Format(seconds);

        // Assert
        Assert.That(result, Is.EqualTo("1s"));
    }

    [Test]
    public void Format_WithSecondsOnly_ReturnsSecondsFormat()
    {
        // Arrange
        const long seconds = 45;

        // Act
        var result = DurationFormatter.Format(seconds);

        // Assert
        Assert.That(result, Is.EqualTo("45s"));
    }

    #endregion

    #region Minutes and Seconds Tests

    [Test]
    public void Format_WithOneMinute_ReturnsMinuteFormat()
    {
        // Arrange
        const long seconds = 60;

        // Act
        var result = DurationFormatter.Format(seconds);

        // Assert
        Assert.That(result, Is.EqualTo("1m 0s"));
    }

    [Test]
    public void Format_WithMinuteAndSeconds_ReturnsMinutesSecondsFormat()
    {
        // Arrange
        const long seconds = 125; // 2 minutes 5 seconds

        // Act
        var result = DurationFormatter.Format(seconds);

        // Assert
        Assert.That(result, Is.EqualTo("2m 5s"));
    }

    [Test]
    public void Format_WithMultipleMinutes_ReturnsCorrectFormat()
    {
        // Arrange
        const long seconds = 325; // 5 minutes 25 seconds

        // Act
        var result = DurationFormatter.Format(seconds);

        // Assert
        Assert.That(result, Is.EqualTo("5m 25s"));
    }

    [Test]
    public void Format_With59MinutesAnd59Seconds_ReturnsMinutesFormat()
    {
        // Arrange
        const long seconds = 3599; // 59m 59s

        // Act
        var result = DurationFormatter.Format(seconds);

        // Assert
        Assert.That(result, Is.EqualTo("59m 59s"));
    }

    #endregion

    #region Hours, Minutes and Seconds Tests

    [Test]
    public void Format_WithOneHour_ReturnsHourFormat()
    {
        // Arrange
        const long seconds = 3600; // 1 hour

        // Act
        var result = DurationFormatter.Format(seconds);

        // Assert
        Assert.That(result, Is.EqualTo("1h 0m 0s"));
    }

    [Test]
    public void Format_WithHourAndMinutes_ReturnsHourMinutesFormat()
    {
        // Arrange
        const long seconds = 3725; // 1h 2m 5s

        // Act
        var result = DurationFormatter.Format(seconds);

        // Assert
        Assert.That(result, Is.EqualTo("1h 2m 5s"));
    }

    [Test]
    public void Format_WithMultipleHours_ReturnsHourFormat()
    {
        // Arrange
        const long seconds = 7325; // 2h 2m 5s

        // Act
        var result = DurationFormatter.Format(seconds);

        // Assert
        Assert.That(result, Is.EqualTo("2h 2m 5s"));
    }

    [Test]
    public void Format_WithLargeHours_ReturnsCorrectFormat()
    {
        // Arrange
        const long seconds = 86399; // 23h 59m 59s

        // Act
        var result = DurationFormatter.Format(seconds);

        // Assert
        Assert.That(result, Is.EqualTo("23h 59m 59s"));
    }

    [Test]
    public void Format_WithMoreThan24Hours_ReturnsCorrectFormat()
    {
        // Arrange
        const long seconds = 90061; // 25h 1m 1s

        // Act
        var result = DurationFormatter.Format(seconds);

        // Assert
        Assert.That(result, Is.EqualTo("25h 1m 1s"));
    }

    #endregion

    #region Edge Cases Tests

    [Test]
    public void Format_WithNegativeSeconds_ThrowsArgumentException()
    {
        // Arrange
        const long seconds = -1;

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => DurationFormatter.Format(seconds));
        Assert.That(ex!.Message, Does.Contain("negative"));
    }

    [Test]
    public void Format_WithVeryLargeNumber_ReturnsCorrectFormat()
    {
        // Arrange
        const long seconds = 1000000; // ~11.57 days

        // Act
        var result = DurationFormatter.Format(seconds);

        // Assert
        Assert.That(result, Is.EqualTo("277h 46m 40s"));
    }

    [Test]
    public void Format_WithMaxLongValue_ReturnsCorrectFormat()
    {
        // Arrange - Long.MaxValue / 3600 = 256204778h 48m 5s
        const long seconds = 922337203685475807; // Near max long

        // Act
        var result = DurationFormatter.Format(seconds);

        // Assert - Just verify it returns a string with expected format
        Assert.That(result, Does.Match(@"^\d+h \d+m \d+s$"));
    }

    #endregion

    #region Boundary Tests

    [Test]
    [TestCase(1, "1s")]
    [TestCase(59, "59s")]
    [TestCase(60, "1m 0s")]
    [TestCase(61, "1m 1s")]
    [TestCase(3599, "59m 59s")]
    [TestCase(3600, "1h 0m 0s")]
    [TestCase(3601, "1h 0m 1s")]
    [TestCase(7199, "1h 59m 59s")]
    [TestCase(7200, "2h 0m 0s")]
    public void Format_WithVariousValues_ReturnsCorrectFormat(long seconds, string expected)
    {
        // Act
        var result = DurationFormatter.Format(seconds);

        // Assert
        Assert.That(result, Is.EqualTo(expected));
    }

    #endregion

    #region Calculation Accuracy Tests

    [Test]
    public void Format_VerifiesHourCalculation_IsCorrect()
    {
        // Arrange
        const long seconds = 14425; // 4h 0m 25s

        // Act
        var result = DurationFormatter.Format(seconds);

        // Assert
        Assert.That(result, Is.EqualTo("4h 0m 25s"));
    }

    [Test]
    public void Format_VerifiesMinuteCalculation_IsCorrect()
    {
        // Arrange
        const long seconds = 305; // 5m 5s

        // Act
        var result = DurationFormatter.Format(seconds);

        // Assert
        Assert.That(result, Is.EqualTo("5m 5s"));
    }

    [Test]
    public void Format_VerifiesSecondCalculation_IsCorrect()
    {
        // Arrange
        const long seconds = 3665; // 1h 1m 5s

        // Act
        var result = DurationFormatter.Format(seconds);

        // Assert - After extracting hours, minutes should be (3665 % 3600) / 60 = 1
        // And seconds should be (3665 % 3600) % 60 = 5
        Assert.That(result, Is.EqualTo("1h 1m 5s"));
    }

    #endregion

    #region Type Consistency Tests

    [Test]
    public void Format_ReturnType_IsString()
    {
        // Act
        var result = DurationFormatter.Format(100);

        // Assert
        Assert.That(result, Is.TypeOf<string>());
    }

    [Test]
    public void Format_ReturnValue_IsNotEmpty()
    {
        // Act
        var result = DurationFormatter.Format(100);

        // Assert
        Assert.That(result, Is.Not.Empty);
    }

    #endregion

    #region Regression Tests

    [Test]
    public void Format_ConsistentBehavior_AcrossMultipleCalls()
    {
        // Arrange
        const long seconds = 3725;
        const string expected = "1h 2m 5s";

        // Act
        var result1 = DurationFormatter.Format(seconds);
        var result2 = DurationFormatter.Format(seconds);

        // Assert
        Assert.That(result1, Is.EqualTo(expected));
        Assert.That(result2, Is.EqualTo(expected));
        Assert.That(result1, Is.EqualTo(result2));
    }

    #endregion
}
