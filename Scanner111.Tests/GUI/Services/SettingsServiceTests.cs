using FluentAssertions;
using Scanner111.Core.Models;
using Scanner111.GUI.Models;
using Scanner111.GUI.Services;

namespace Scanner111.Tests.GUI.Services;

/// <summary>
///     Tests for SettingsService implementation.
///     These tests verify that the service correctly handles loading, saving, and mapping
///     between ApplicationSettings and UserSettings.
/// </summary>
[Collection("Settings Tests")]
public class SettingsServiceTests : IDisposable
{
    private readonly SettingsService _service;
    private readonly string _settingsPath;
    private readonly string _testDirectory;

    public SettingsServiceTests()
    {
        _service = new SettingsService();
        _testDirectory = Path.Combine(Path.GetTempPath(), $"Scanner111Test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        _settingsPath = Path.Combine(_testDirectory, "settings.json");

        // Set the settings path for testing - the environment variable expects a directory, not a file path
        Environment.SetEnvironmentVariable("SCANNER111_SETTINGS_PATH", _testDirectory);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("SCANNER111_SETTINGS_PATH", null);
        if (Directory.Exists(_testDirectory))
            try
            {
                Directory.Delete(_testDirectory, true);
            }
            catch
            {
                // Ignore cleanup errors in tests
            }
    }

    [Fact]
    public async Task LoadSettingsAsync_ReturnsDefaultSettingsWhenFileDoesNotExist()
    {
        // Act
        var settings = await _service.LoadSettingsAsync();

        // Assert
        settings.Should().NotBeNull("because default settings should be returned");
        settings.DefaultLogPath.Should().BeEmpty("because default log path should be empty");
        settings.AutoLoadF4SeLogs.Should().BeTrue("because auto-load should default to true");
        settings.MaxLogMessages.Should().Be(100, "because max log messages should default to 100");
        settings.WindowWidth.Should().Be(1200, "because window width should default to 1200");
        settings.WindowHeight.Should().Be(800, "because window height should default to 800");
    }

    [Fact]
    public async Task SaveSettingsAsync_PersistsSettingsToDisk()
    {
        // Arrange
        var settings = new ApplicationSettings
        {
            DefaultLogPath = @"C:\Test\custom.log",
            DefaultGamePath = @"C:\Games\Custom",
            AutoLoadF4SeLogs = false,
            MaxLogMessages = 200,
            WindowWidth = 1920,
            WindowHeight = 1080
        };

        // Act
        await _service.SaveSettingsAsync(settings);

        // Assert
        File.Exists(_settingsPath).Should().BeTrue("because settings file should be created");

        // Load settings again to verify persistence
        var loadedSettings = await _service.LoadSettingsAsync();
        loadedSettings.DefaultLogPath.Should().Be(@"C:\Test\custom.log", "because saved path should persist");
        loadedSettings.DefaultGamePath.Should().Be(@"C:\Games\Custom", "because saved game path should persist");
        loadedSettings.AutoLoadF4SeLogs.Should().BeFalse("because saved auto-load setting should persist");
        loadedSettings.MaxLogMessages.Should().Be(200, "because saved max messages should persist");
        loadedSettings.WindowWidth.Should().Be(1920, "because saved window width should persist");
        loadedSettings.WindowHeight.Should().Be(1080, "because saved window height should persist");
    }

    [Fact]
    public void GetDefaultSettings_ReturnsConsistentDefaults()
    {
        // Act
        var defaults1 = _service.GetDefaultSettings();
        var defaults2 = _service.GetDefaultSettings();

        // Assert
        defaults1.Should().NotBeSameAs(defaults2, "because new instance should be created each time");
        defaults1.Should().BeEquivalentTo(defaults2, "because default values should be consistent");

        // Verify specific defaults
        defaults1.DefaultLogPath.Should().BeEmpty("because default log path should be empty");
        defaults1.AutoLoadF4SeLogs.Should().BeTrue("because auto-load should default to true");
        defaults1.MaxLogMessages.Should().Be(100, "because max log messages should default to 100");
        defaults1.EnableProgressNotifications.Should().BeTrue("because progress notifications should default to true");
        defaults1.RememberWindowSize.Should().BeTrue("because remember window size should default to true");
        defaults1.WindowWidth.Should().Be(1200, "because window width should default to 1200");
        defaults1.WindowHeight.Should().Be(800, "because window height should default to 800");
        defaults1.EnableDebugLogging.Should().BeFalse("because debug logging should default to false");
        defaults1.MaxRecentItems.Should().Be(10, "because max recent items should default to 10");
        defaults1.AutoSaveResults.Should().BeTrue("because auto-save should default to true");
        defaults1.DefaultOutputFormat.Should().Be("detailed", "because default output format should be detailed");
    }

    [Fact]
    public async Task LoadUserSettingsAsync_MapsApplicationSettingsToUserSettings()
    {
        // Arrange
        var appSettings = new ApplicationSettings
        {
            DefaultLogPath = @"C:\Test\user.log",
            DefaultGamePath = @"C:\Games\UserGame",
            AutoLoadF4SeLogs = false,
            MaxLogMessages = 150,
            EnableUpdateCheck = true,
            UpdateSource = "GitHub"
        };
        await _service.SaveSettingsAsync(appSettings);

        // Act
        var userSettings = await _service.LoadUserSettingsAsync();

        // Assert
        userSettings.Should().NotBeNull("because user settings should be created");
        userSettings.DefaultLogPath.Should().Be(@"C:\Test\user.log", "because path should be mapped");
        userSettings.DefaultGamePath.Should().Be(@"C:\Games\UserGame", "because game path should be mapped");
        userSettings.AutoLoadF4SeLogs.Should().BeFalse("because auto-load should be mapped");
        userSettings.MaxLogMessages.Should().Be(150, "because max messages should be mapped");
        userSettings.EnableUpdateCheck.Should().BeTrue("because update check should be mapped");
        userSettings.UpdateSource.Should().Be("GitHub", "because update source should be mapped");

        // UserSettings specific properties
        userSettings.FcxMode.Should().BeFalse("because FCX mode should default to false");
        userSettings.MoveUnsolvedLogs.Should().BeFalse("because move unsolved logs should default to false");
    }

    [Fact]
    public async Task SaveUserSettingsAsync_MapsUserSettingsToApplicationSettings()
    {
        // Arrange
        var userSettings = new UserSettings
        {
            DefaultLogPath = @"C:\Test\mapped.log",
            DefaultGamePath = @"C:\Games\MappedGame",
            AutoLoadF4SeLogs = true,
            MaxLogMessages = 75,
            EnableUpdateCheck = false,
            UpdateSource = "Nexus",
            FcxMode = true,
            MoveUnsolvedLogs = true,
            ModsFolder = @"C:\Mods",
            IniFolder = @"C:\INI"
        };

        // Act
        await _service.SaveUserSettingsAsync(userSettings);

        // Assert - Load as ApplicationSettings to verify mapping
        var appSettings = await _service.LoadSettingsAsync();
        appSettings.DefaultLogPath.Should().Be(@"C:\Test\mapped.log", "because path should be mapped");
        appSettings.DefaultGamePath.Should().Be(@"C:\Games\MappedGame", "because game path should be mapped");
        appSettings.AutoLoadF4SeLogs.Should().BeTrue("because auto-load should be mapped");
        appSettings.MaxLogMessages.Should().Be(75, "because max messages should be mapped");
        appSettings.EnableUpdateCheck.Should().BeFalse("because update check should be mapped");
        appSettings.UpdateSource.Should().Be("Nexus", "because update source should be mapped");

        // Note: FcxMode and MoveUnsolvedLogs are UserSettings-specific and not in ApplicationSettings
    }

    [Fact]
    public async Task RoundTrip_ApplicationSettings_PreservesAllValues()
    {
        // Arrange
        var original = new ApplicationSettings
        {
            DefaultLogPath = @"C:\Test\roundtrip.log",
            DefaultGamePath = @"C:\Games\RoundTrip",
            DefaultScanDirectory = @"C:\Scans\RoundTrip",
            AutoLoadF4SeLogs = false,
            MaxLogMessages = 123,
            EnableProgressNotifications = false,
            RememberWindowSize = false,
            WindowWidth = 1600,
            WindowHeight = 900,
            EnableDebugLogging = true,
            MaxRecentItems = 15,
            AutoSaveResults = true,
            DefaultOutputFormat = "json",
            CrashLogsDirectory = @"C:\CrashLogs",
            SkipXseCopy = true,
            EnableUpdateCheck = true,
            UpdateSource = "Both"
        };

        // Act
        await _service.SaveSettingsAsync(original);
        var loaded = await _service.LoadSettingsAsync();

        // Assert
        loaded.Should().BeEquivalentTo(original, "because all settings should round-trip correctly");
    }

    [Fact]
    public async Task RoundTrip_UserSettings_PreservesAllValues()
    {
        // Arrange
        var original = new UserSettings
        {
            DefaultLogPath = @"C:\Test\user_roundtrip.log",
            DefaultGamePath = @"C:\Games\UserRoundTrip",
            DefaultScanDirectory = @"C:\Scans\UserRoundTrip",
            AutoLoadF4SeLogs = true,
            MaxLogMessages = 456,
            EnableProgressNotifications = true,
            RememberWindowSize = true,
            WindowWidth = 2560,
            WindowHeight = 1440,
            EnableDebugLogging = false,
            MaxRecentItems = 20,
            AutoSaveResults = false,
            DefaultOutputFormat = "markdown",
            CrashLogsDirectory = @"C:\UserCrashLogs",
            SkipXseCopy = false,
            EnableUpdateCheck = false,
            UpdateSource = "GitHub",
            FcxMode = true,
            MoveUnsolvedLogs = true,
            ModsFolder = @"C:\UserMods",
            IniFolder = @"C:\UserINI"
        };

        // Add recent files
        original.RecentLogFiles.Add(@"C:\recent1.log");
        original.RecentLogFiles.Add(@"C:\recent2.log");
        original.RecentGamePaths.Add(@"C:\Games\Recent1");
        original.RecentScanDirectories.Add(@"C:\Scans\Recent1");

        // Act
        await _service.SaveUserSettingsAsync(original);
        var loaded = await _service.LoadUserSettingsAsync();

        // Assert
        loaded.Should().BeEquivalentTo(original, options => options
                .Excluding(s => s.RecentLogFiles)
                .Excluding(s => s.RecentGamePaths)
                .Excluding(s => s.RecentScanDirectories),
            "because all basic settings should round-trip correctly");

        // Check collections separately
        loaded.RecentLogFiles.Should().BeEquivalentTo(original.RecentLogFiles,
            "because recent log files should be preserved");
        loaded.RecentGamePaths.Should().BeEquivalentTo(original.RecentGamePaths,
            "because recent game paths should be preserved");
        loaded.RecentScanDirectories.Should().BeEquivalentTo(original.RecentScanDirectories,
            "because recent scan directories should be preserved");
    }

    [Fact]
    public async Task LoadSettingsAsync_HandlesCorruptedFile()
    {
        // Arrange - Write invalid JSON to settings file
        var actualSettingsPath = Path.Combine(_testDirectory, "settings.json");
        await File.WriteAllTextAsync(actualSettingsPath, "{ invalid json content }");

        // Act
        var settings = await _service.LoadSettingsAsync();

        // Assert
        settings.Should().NotBeNull("because default settings should be returned on error");
        settings.Should().BeEquivalentTo(_service.GetDefaultSettings(),
            "because corrupted file should result in default settings");
    }

    [Fact]
    public async Task SaveSettingsAsync_CreatesDirectoryIfNotExists()
    {
        // Arrange
        var nestedDir = Path.Combine(_testDirectory, "nested", "deep");
        Environment.SetEnvironmentVariable("SCANNER111_SETTINGS_PATH", nestedDir);

        var settings = new ApplicationSettings
        {
            DefaultLogPath = @"C:\Test\nested.log"
        };

        // Act
        await _service.SaveSettingsAsync(settings);

        // Assert
        var nestedPath = Path.Combine(nestedDir, "settings.json");
        File.Exists(nestedPath).Should().BeTrue("because directory should be created if needed");
    }

    [Fact]
    public async Task ConcurrentAccess_HandledGracefully()
    {
        // Arrange
        var settings1 = new ApplicationSettings { DefaultLogPath = @"C:\Test\concurrent1.log" };
        var settings2 = new ApplicationSettings { DefaultLogPath = @"C:\Test\concurrent2.log" };

        // Act - Attempt concurrent saves
        var task1 = _service.SaveSettingsAsync(settings1);
        var task2 = _service.SaveSettingsAsync(settings2);

        await Task.WhenAll(task1, task2);

        // Assert - One of the settings should have won
        var loaded = await _service.LoadSettingsAsync();
        loaded.DefaultLogPath.Should().BeOneOf(
            @"C:\Test\concurrent1.log",
            @"C:\Test\concurrent2.log",
            "because one of the concurrent saves should succeed");
    }

    [Fact]
    public async Task UserSettings_RecentCollections_InitializedAsEmpty()
    {
        // Act
        var userSettings = await _service.LoadUserSettingsAsync();

        // Assert
        userSettings.RecentLogFiles.Should().NotBeNull("because collection should be initialized");
        userSettings.RecentLogFiles.Should().BeEmpty("because no recent files exist initially");
        userSettings.RecentGamePaths.Should().NotBeNull("because collection should be initialized");
        userSettings.RecentGamePaths.Should().BeEmpty("because no recent paths exist initially");
        userSettings.RecentScanDirectories.Should().NotBeNull("because collection should be initialized");
        userSettings.RecentScanDirectories.Should().BeEmpty("because no recent directories exist initially");
    }

    [Fact]
    public async Task SaveUserSettingsAsync_PreservesRecentItemsLimit()
    {
        // Arrange
        var userSettings = new UserSettings
        {
            MaxRecentItems = 5
        };

        // Add more items than the limit
        for (var i = 0; i < 10; i++) userSettings.RecentLogFiles.Add($@"C:\log{i}.log");

        // Act
        await _service.SaveUserSettingsAsync(userSettings);
        var loaded = await _service.LoadUserSettingsAsync();

        // Assert
        loaded.MaxRecentItems.Should().Be(5, "because max recent items should be preserved");
        // Note: The actual limiting of recent items would be handled by the ViewModel or UI layer
    }
}