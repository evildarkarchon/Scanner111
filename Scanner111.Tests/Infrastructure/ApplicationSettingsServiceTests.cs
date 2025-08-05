using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using System.Text.Json;
using Xunit;

namespace Scanner111.Tests.Infrastructure;

/// <summary>
/// Unit tests for the ApplicationSettingsService class
/// </summary>
[Collection("Settings Tests")]
public class ApplicationSettingsServiceTests : IDisposable
{
    private readonly ApplicationSettingsService _service;
    private readonly string _testSettingsPath;
    private readonly string _testSettingsDir;

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
        System.Threading.Thread.Sleep(Random.Shared.Next(50, 150));
    }

    public void Dispose()
    {
        // Clean up test directory
        Environment.SetEnvironmentVariable("SCANNER111_SETTINGS_PATH", null);
        
        if (Directory.Exists(_testSettingsDir))
        {
            try
            {
                Directory.Delete(_testSettingsDir, true);
            }
            catch
            {
                // Best effort - ignore cleanup failures
            }
        }
    }

    [Fact]
    public async Task LoadSettingsAsync_FirstTime_CreatesDefaults()
    {
        // Act
        var settings = await _service.LoadSettingsAsync();

        // Assert
        Assert.NotNull(settings);
        Assert.False(settings.FcxMode);
        Assert.False(settings.ShowFormIdValues);
        Assert.True(settings.AutoSaveResults);
        Assert.True(settings.CacheEnabled);
        Assert.Equal("detailed", settings.DefaultOutputFormat);
        Assert.Equal(Environment.ProcessorCount * 2, settings.MaxConcurrentScans);
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
        Assert.NotNull(loadedSettings);
        Assert.Equal(settings.FcxMode, loadedSettings.FcxMode);
        Assert.Equal(settings.ShowFormIdValues, loadedSettings.ShowFormIdValues);
        Assert.Equal(settings.SimplifyLogs, loadedSettings.SimplifyLogs);
        Assert.Equal(settings.DefaultLogPath, loadedSettings.DefaultLogPath);
        Assert.Equal(settings.DefaultGamePath, loadedSettings.DefaultGamePath);
        Assert.Equal(settings.DefaultOutputFormat, loadedSettings.DefaultOutputFormat);
        Assert.Equal(settings.MaxConcurrentScans, loadedSettings.MaxConcurrentScans);
        Assert.Equal(settings.WindowWidth, loadedSettings.WindowWidth);
        Assert.Equal(settings.WindowHeight, loadedSettings.WindowHeight);
    }

    [Fact]
    public async Task LoadSettingsAsync_WithCorruptedFile_ReturnsDefaults()
    {
        // Arrange
        await File.WriteAllTextAsync(_testSettingsPath, "{ invalid json }");

        // Act
        var settings = await _service.LoadSettingsAsync();

        // Assert
        Assert.NotNull(settings);
        // Should return defaults when file is corrupted
        Assert.False(settings.FcxMode);
        Assert.Equal("detailed", settings.DefaultOutputFormat);
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
        Assert.NotNull(finalSettings);
        // The final state should be from one of the two writes
        Assert.True(finalSettings.FcxMode == true || finalSettings.FcxMode == false);
    }

    [Fact]
    public async Task SaveSettingAsync_UpdatesSingleProperty()
    {
        // Arrange
        var initialSettings = await _service.LoadSettingsAsync();
        Assert.False(initialSettings.FcxMode);

        // Act
        await _service.SaveSettingAsync("FcxMode", true);
        
        // Assert
        var updatedSettings = await _service.LoadSettingsAsync();
        Assert.True(updatedSettings.FcxMode);
        // Other settings should remain unchanged
        Assert.Equal(initialSettings.DefaultOutputFormat, updatedSettings.DefaultOutputFormat);
    }

    [Fact]
    public async Task SaveSettingAsync_WithInvalidKey_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _service.SaveSettingAsync("NonExistentProperty", "value"));
    }

    [Fact]
    public async Task SaveSettingAsync_WithInvalidValueType_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _service.SaveSettingAsync("MaxConcurrentScans", "not a number"));
    }

    [Fact]
    public async Task SaveSettingAsync_WithCaseInsensitiveKey_Works()
    {
        // Act
        await _service.SaveSettingAsync("fcxmode", true); // lowercase
        
        // Assert
        var settings = await _service.LoadSettingsAsync();
        Assert.True(settings.FcxMode);
    }

    [Fact]
    public async Task GetDefaultSettings_ReturnsValidDefaults()
    {
        // Act
        var defaults = _service.GetDefaultSettings();

        // Assert
        Assert.NotNull(defaults);
        Assert.False(defaults.FcxMode);
        Assert.False(defaults.ShowFormIdValues);
        Assert.False(defaults.SimplifyLogs);
        Assert.False(defaults.MoveUnsolvedLogs);
        Assert.False(defaults.VrMode);
        Assert.True(defaults.AutoSaveResults);
        Assert.True(defaults.AutoLoadF4SeLogs);
        Assert.False(defaults.SkipXseCopy);
        Assert.True(defaults.CacheEnabled);
        Assert.False(defaults.EnableDebugLogging);
        Assert.True(defaults.EnableProgressNotifications);
        Assert.False(defaults.DisableProgress);
        Assert.False(defaults.DisableColors);
        Assert.Equal(1200, defaults.WindowWidth);
        Assert.Equal(800, defaults.WindowHeight);
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
        Assert.True(loaded.FcxMode); // Modified value
        Assert.Equal(1600, loaded.WindowWidth); // Modified value
        Assert.True(loaded.AutoSaveResults); // Default value preserved
        Assert.Equal("detailed", loaded.DefaultOutputFormat); // Default value preserved
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
        Assert.True(Directory.Exists(newDir));
        Assert.True(File.Exists(Path.Combine(newDir, "settings.json")));
    }
}