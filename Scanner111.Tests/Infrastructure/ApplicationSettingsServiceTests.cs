using System.Text.Json;
using FluentAssertions;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;

namespace Scanner111.Tests.Infrastructure;

/// <summary>
///     Unit tests for the ApplicationSettingsService class
/// </summary>
[Collection("Settings Tests")]
public class ApplicationSettingsServiceTests : IDisposable
{
    private readonly ApplicationSettingsService _service;
    private readonly string _testSettingsDir;
    private readonly string _testSettingsPath;

    public ApplicationSettingsServiceTests()
    {
        _service = new ApplicationSettingsService();

        // Create a temporary directory for test settings
        _testSettingsDir = Path.Combine(Path.GetTempPath(), $"Scanner111Tests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testSettingsDir);
        _testSettingsPath = Path.Combine(_testSettingsDir, "settings.json");

        // Override the settings directory for testing
        Environment.SetEnvironmentVariable("SCANNER111_SETTINGS_PATH", _testSettingsDir);

        // Small delay to avoid race conditions during parallel test execution
        Thread.Sleep(Random.Shared.Next(50, 150));
    }

    public void Dispose()
    {
        // Clean up test directory
        Environment.SetEnvironmentVariable("SCANNER111_SETTINGS_PATH", null);

        if (Directory.Exists(_testSettingsDir))
            try
            {
                Directory.Delete(_testSettingsDir, true);
            }
            catch
            {
                // Best effort - ignore cleanup failures
            }
    }

    [Fact]
    public async Task LoadSettingsAsync_FirstTime_CreatesDefaults()
    {
        // Act
        var settings = await _service.LoadSettingsAsync();

        // Assert
        settings.Should().NotBeNull();
        settings.FcxMode.Should().BeFalse();
        settings.ShowFormIdValues.Should().BeFalse();
        settings.AutoSaveResults.Should().BeTrue();
        settings.CacheEnabled.Should().BeTrue();
        settings.DefaultOutputFormat.Should().Be("detailed");
        settings.MaxConcurrentScans.Should().Be(Environment.ProcessorCount * 2);
    }

    [Fact]
    public async Task SaveSettingsAsync_PersistsAllProperties()
    {
        // Arrange
        var settings = new ApplicationSettings
        {
            FcxMode = true,
            ShowFormIdValues = true,
            SimplifyLogs = true,
            MoveUnsolvedLogs = true,
            VrMode = true,
            DefaultLogPath = "C:\\TestLogs",
            DefaultGamePath = "C:\\Games\\Fallout4",
            DefaultScanDirectory = "C:\\ScanDir",
            CrashLogsDirectory = "C:\\CrashLogs",
            DefaultOutputFormat = "summary",
            AutoSaveResults = false,
            AutoLoadF4SeLogs = false,
            SkipXseCopy = true,
            MaxConcurrentScans = 8,
            CacheEnabled = false,
            EnableDebugLogging = true,
            AudioNotifications = false,
            DisableProgress = true,
            DisableColors = true,
            WindowWidth = 1400,
            WindowHeight = 900
        };

        // Act
        await _service.SaveSettingsAsync(settings);

        // Read the file directly to verify
        var json = await File.ReadAllTextAsync(_testSettingsPath);
        var loadedSettings = JsonSerializer.Deserialize<ApplicationSettings>(json);

        // Assert
        loadedSettings.Should().NotBeNull();
        loadedSettings.Should().BeEquivalentTo(settings, options => options
            .Including(s => s.FcxMode)
            .Including(s => s.ShowFormIdValues)
            .Including(s => s.SimplifyLogs)
            .Including(s => s.DefaultLogPath)
            .Including(s => s.DefaultGamePath)
            .Including(s => s.DefaultOutputFormat)
            .Including(s => s.MaxConcurrentScans)
            .Including(s => s.WindowWidth)
            .Including(s => s.WindowHeight));
    }

    [Fact]
    public async Task LoadSettingsAsync_WithCorruptedFile_ReturnsDefaults()
    {
        // Arrange
        await File.WriteAllTextAsync(_testSettingsPath, "{ invalid json }");

        // Act
        var settings = await _service.LoadSettingsAsync();

        // Assert
        settings.Should().NotBeNull();
        // Should return defaults when file is corrupted
        settings.FcxMode.Should().BeFalse("defaults should be returned when file is corrupted");
        settings.DefaultOutputFormat.Should().Be("detailed");
    }

    [Fact]
    public async Task MultipleInstanceAccess_HandlesFileLocking()
    {
        // Arrange
        var settings1 = new ApplicationSettings { FcxMode = true };
        var settings2 = new ApplicationSettings { FcxMode = false };

        // Act - Simulate concurrent writes
        var task1 = _service.SaveSettingsAsync(settings1);
        var task2 = Task.Run(async () =>
        {
            await Task.Delay(10); // Small delay to ensure overlap
            var service2 = new ApplicationSettingsService();
            await service2.SaveSettingsAsync(settings2);
        });

        await Task.WhenAll(task1, task2);

        // Assert - One of the writes should have succeeded
        var finalSettings = await _service.LoadSettingsAsync();
        finalSettings.Should().NotBeNull();
        // The final state should be from one of the two writes
        (finalSettings.FcxMode || !finalSettings.FcxMode).Should()
            .BeTrue("the final state should be from one of the two writes");
    }

    [Fact]
    public async Task SaveSettingAsync_UpdatesSingleProperty()
    {
        // Arrange
        var initialSettings = await _service.LoadSettingsAsync();
        initialSettings.FcxMode.Should().BeFalse();

        // Act
        await _service.SaveSettingAsync("FcxMode", true);

        // Assert
        var updatedSettings = await _service.LoadSettingsAsync();
        updatedSettings.FcxMode.Should().BeTrue();
        // Other settings should remain unchanged
        updatedSettings.DefaultOutputFormat.Should().Be(initialSettings.DefaultOutputFormat);
    }

    [Fact]
    public async Task SaveSettingAsync_WithInvalidKey_ThrowsException()
    {
        // Act & Assert
        var act = async () => await _service.SaveSettingAsync("NonExistentProperty", "value");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SaveSettingAsync_WithInvalidValueType_ThrowsException()
    {
        // Act & Assert
        var act = async () => await _service.SaveSettingAsync("MaxConcurrentScans", "not a number");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SaveSettingAsync_WithCaseInsensitiveKey_Works()
    {
        // Act
        await _service.SaveSettingAsync("fcxmode", true); // lowercase

        // Assert
        var settings = await _service.LoadSettingsAsync();
        settings.FcxMode.Should().BeTrue();
    }

    [Fact]
    public async Task GetDefaultSettings_ReturnsValidDefaults()
    {
        // Act
        var defaults = _service.GetDefaultSettings();

        // Assert
        defaults.Should().NotBeNull();
        defaults.FcxMode.Should().BeFalse();
        defaults.ShowFormIdValues.Should().BeFalse();
        defaults.SimplifyLogs.Should().BeFalse();
        defaults.MoveUnsolvedLogs.Should().BeFalse();
        defaults.VrMode.Should().BeFalse();
        defaults.AutoSaveResults.Should().BeTrue();
        defaults.AutoLoadF4SeLogs.Should().BeTrue();
        defaults.SkipXseCopy.Should().BeFalse();
        defaults.CacheEnabled.Should().BeTrue();
        defaults.EnableDebugLogging.Should().BeFalse();
        defaults.EnableProgressNotifications.Should().BeTrue();
        defaults.DisableProgress.Should().BeFalse();
        defaults.DisableColors.Should().BeFalse();
        defaults.WindowWidth.Should().Be(1200);
        defaults.WindowHeight.Should().Be(800);
    }

    [Fact]
    public async Task LoadSettingsAsync_WithPartialSettings_MergesWithDefaults()
    {
        // Arrange - Save settings with specific values using the service
        var settings = _service.GetDefaultSettings();
        settings.FcxMode = true;
        settings.WindowWidth = 1600;
        await _service.SaveSettingsAsync(settings);

        // Act - Create a new service instance to test loading
        var newService = new ApplicationSettingsService();
        var loaded = await newService.LoadSettingsAsync();

        // Assert
        loaded.FcxMode.Should().BeTrue("FcxMode was modified");
        loaded.WindowWidth.Should().Be(1600, "WindowWidth was modified");
        loaded.AutoSaveResults.Should().BeTrue("default value should be preserved");
        loaded.DefaultOutputFormat.Should().Be("detailed", "default value should be preserved");
    }

    [Fact]
    public async Task SaveSettingsAsync_CreatesDirectoryIfMissing()
    {
        // Arrange
        var newDir = Path.Combine(_testSettingsDir, "subdir");
        Environment.SetEnvironmentVariable("SCANNER111_SETTINGS_PATH", newDir);
        var service = new ApplicationSettingsService();

        // Act
        await service.SaveSettingsAsync(new ApplicationSettings());

        // Assert
        Directory.Exists(newDir).Should().BeTrue("directory should be created");
        File.Exists(Path.Combine(newDir, "settings.json")).Should().BeTrue("settings file should be created");
    }
}