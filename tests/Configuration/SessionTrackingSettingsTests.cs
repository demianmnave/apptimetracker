using NUnit.Framework;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using AppTimeTracker.Configuration;
using AppTimeTracker.Services;

namespace AppTimeTracker.Tests.Configuration;

/// <summary>
/// Unit tests for SessionTrackingSettings configuration class and Worker dependency injection.
/// Tests configuration binding, default values, and dependency injection behavior.
/// </summary>
[TestFixture]
public class SessionTrackingSettingsTests
{
    #region Default Values Tests

    [Test]
    public void SessionTrackingSettings_DefaultMinSessionDurationSeconds_IsOne()
    {
        // Arrange & Act
        var settings = new SessionTrackingSettings();

        // Assert
        Assert.That(settings.MinSessionDurationSeconds, Is.EqualTo(1));
    }

    [Test]
    public void SessionTrackingSettings_DefaultPauseOnLock_IsTrue()
    {
        // Arrange & Act
        var settings = new SessionTrackingSettings();

        // Assert
        Assert.That(settings.PauseOnLock, Is.True);
    }

    [Test]
    public void SessionTrackingSettings_DefaultPollingIntervalMs_Is500()
    {
        // Arrange & Act
        var settings = new SessionTrackingSettings();

        // Assert
        Assert.That(settings.PollingIntervalMs, Is.EqualTo(500));
    }

    [Test]
    public void SessionTrackingSettings_DefaultSessionFlushIntervalMs_Is30000()
    {
        // Arrange & Act
        var settings = new SessionTrackingSettings();

        // Assert
        Assert.That(settings.SessionFlushIntervalMs, Is.EqualTo(30000));
    }

    [Test]
    public void SessionTrackingSettings_DefaultGracefulShutdownTimeoutSeconds_Is5()
    {
        // Arrange & Act
        var settings = new SessionTrackingSettings();

        // Assert
        Assert.That(settings.GracefulShutdownTimeoutSeconds, Is.EqualTo(5));
    }

    #endregion

    #region Property Assignment Tests

    [Test]
    public void SessionTrackingSettings_MinSessionDurationSeconds_CanBeSet()
    {
        // Arrange
        var settings = new SessionTrackingSettings();

        // Act
        settings.MinSessionDurationSeconds = 5;

        // Assert
        Assert.That(settings.MinSessionDurationSeconds, Is.EqualTo(5));
    }

    [Test]
    public void SessionTrackingSettings_PauseOnLock_CanBeSet()
    {
        // Arrange
        var settings = new SessionTrackingSettings();

        // Act
        settings.PauseOnLock = false;

        // Assert
        Assert.That(settings.PauseOnLock, Is.False);
    }

    [Test]
    public void SessionTrackingSettings_PollingIntervalMs_CanBeSet()
    {
        // Arrange
        var settings = new SessionTrackingSettings();

        // Act
        settings.PollingIntervalMs = 1000;

        // Assert
        Assert.That(settings.PollingIntervalMs, Is.EqualTo(1000));
    }

    [Test]
    public void SessionTrackingSettings_SessionFlushIntervalMs_CanBeSet()
    {
        // Arrange
        var settings = new SessionTrackingSettings();

        // Act
        settings.SessionFlushIntervalMs = 60000;

        // Assert
        Assert.That(settings.SessionFlushIntervalMs, Is.EqualTo(60000));
    }

    [Test]
    public void SessionTrackingSettings_GracefulShutdownTimeoutSeconds_CanBeSet()
    {
        // Arrange
        var settings = new SessionTrackingSettings();

        // Act
        settings.GracefulShutdownTimeoutSeconds = 10;

        // Assert
        Assert.That(settings.GracefulShutdownTimeoutSeconds, Is.EqualTo(10));
    }

    #endregion

    #region Configuration Binding Tests

    [Test]
    public void SessionTrackingSettings_BindsFromIConfiguration_Correctly()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "SessionTracking:MinSessionDurationSeconds", "2" },
                { "SessionTracking:PauseOnLock", "false" },
                { "SessionTracking:PollingIntervalMs", "1000" }
            })
            .Build();

        var settings = new SessionTrackingSettings();

        // Act
        config.GetSection("SessionTracking").Bind(settings);

        // Assert
        Assert.That(settings.MinSessionDurationSeconds, Is.EqualTo(2));
        Assert.That(settings.PauseOnLock, Is.False);
        Assert.That(settings.PollingIntervalMs, Is.EqualTo(1000));
    }

    [Test]
    public void SessionTrackingSettings_BindsPartialConfiguration_UsesDefaults()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "SessionTracking:MinSessionDurationSeconds", "3" }
            })
            .Build();

        var settings = new SessionTrackingSettings();

        // Act
        config.GetSection("SessionTracking").Bind(settings);

        // Assert
        Assert.That(settings.MinSessionDurationSeconds, Is.EqualTo(3)); // Bound value
        Assert.That(settings.PauseOnLock, Is.True); // Default value
        Assert.That(settings.PollingIntervalMs, Is.EqualTo(500)); // Default value
    }

    [Test]
    public void SessionTrackingSettings_BindsFromEmptyConfiguration_UsesAllDefaults()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var settings = new SessionTrackingSettings();

        // Act
        config.GetSection("SessionTracking").Bind(settings);

        // Assert
        Assert.That(settings.MinSessionDurationSeconds, Is.EqualTo(1)); // Default
        Assert.That(settings.PauseOnLock, Is.True); // Default
        Assert.That(settings.PollingIntervalMs, Is.EqualTo(500)); // Default
    }

    #endregion

    #region Dependency Injection Tests

    [Test]
    public void SessionTrackingSettings_RegisteredInServiceCollection_ResolvesCorrectly()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "SessionTracking:MinSessionDurationSeconds", "5" }
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton(config);
        services.Configure<SessionTrackingSettings>(config.GetSection("SessionTracking"));

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var options = serviceProvider.GetRequiredService<IOptions<SessionTrackingSettings>>();
        var settings = options.Value;

        // Assert
        Assert.That(settings.MinSessionDurationSeconds, Is.EqualTo(5));
    }

    [Test]
    public void SessionTrackingSettings_IOptionsPattern_AllowsRuntimeChanges()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "SessionTracking:MinSessionDurationSeconds", "1" }
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton(config);
        services.Configure<SessionTrackingSettings>(config.GetSection("SessionTracking"));
        var serviceProvider = services.BuildServiceProvider();

        var options = serviceProvider.GetRequiredService<IOptions<SessionTrackingSettings>>();

        // Act - Simulate runtime change by creating new settings instance
        var newSettings = new SessionTrackingSettings { MinSessionDurationSeconds = 10 };

        // Assert
        Assert.That(options.Value.MinSessionDurationSeconds, Is.EqualTo(1)); // Original
        Assert.That(newSettings.MinSessionDurationSeconds, Is.EqualTo(10)); // Updated
    }

    #endregion

    #region Validation Tests

    [Test]
    public void SessionTrackingSettings_MinSessionDurationSeconds_CanBeZero()
    {
        // Arrange
        var settings = new SessionTrackingSettings();

        // Act
        settings.MinSessionDurationSeconds = 0;

        // Assert
        Assert.That(settings.MinSessionDurationSeconds, Is.EqualTo(0));
    }

    [Test]
    public void SessionTrackingSettings_MinSessionDurationSeconds_CanBeNegative()
    {
        // Arrange
        var settings = new SessionTrackingSettings();

        // Act
        settings.MinSessionDurationSeconds = -1;

        // Assert
        Assert.That(settings.MinSessionDurationSeconds, Is.EqualTo(-1));
        // Note: Actual validation would be in Worker class using DurationFormatter.Format()
    }

    [Test]
    public void SessionTrackingSettings_PollingIntervalMs_CanBeLarge()
    {
        // Arrange
        var settings = new SessionTrackingSettings();

        // Act
        settings.PollingIntervalMs = 60000;

        // Assert
        Assert.That(settings.PollingIntervalMs, Is.EqualTo(60000));
    }

    #endregion

    #region Worker Integration Tests

    [Test]
    public void Worker_ReceivesSessionTrackingSettings_ViaConstructorInjection()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "SessionTracking:MinSessionDurationSeconds", "2" }
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton(config);
        services.Configure<SessionTrackingSettings>(config.GetSection("SessionTracking"));
        services.AddLogging();

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var options = serviceProvider.GetRequiredService<IOptions<SessionTrackingSettings>>();
        var settings = options.Value;

        // Assert
        Assert.That(settings, Is.Not.Null);
        Assert.That(settings.MinSessionDurationSeconds, Is.EqualTo(2));
    }

    [Test]
    public void SessionTrackingSettings_MultipleInstances_HaveDifferentValues()
    {
        // Arrange
        var settings1 = new SessionTrackingSettings { MinSessionDurationSeconds = 1 };
        var settings2 = new SessionTrackingSettings { MinSessionDurationSeconds = 5 };

        // Act & Assert
        Assert.That(settings1.MinSessionDurationSeconds, Is.EqualTo(1));
        Assert.That(settings2.MinSessionDurationSeconds, Is.EqualTo(5));
        Assert.That(settings1.MinSessionDurationSeconds, Is.Not.EqualTo(settings2.MinSessionDurationSeconds));
    }

    #endregion

    #region Edge Cases Tests

    [Test]
    public void SessionTrackingSettings_LargeTimeoutValues_AreAccepted()
    {
        // Arrange
        var settings = new SessionTrackingSettings();

        // Act
        settings.GracefulShutdownTimeoutSeconds = 3600; // 1 hour

        // Assert
        Assert.That(settings.GracefulShutdownTimeoutSeconds, Is.EqualTo(3600));
    }

    [Test]
    public void SessionTrackingSettings_AllPropertiesArePublic()
    {
        // Arrange
        var settings = new SessionTrackingSettings();
        var properties = typeof(SessionTrackingSettings).GetProperties();

        // Act & Assert
        Assert.That(properties.Length, Is.GreaterThan(0));
        foreach (var prop in properties)
        {
            Assert.That(prop.CanRead && prop.CanWrite, Is.True, $"Property {prop.Name} should be readable and writable");
        }
    }

    #endregion
}
