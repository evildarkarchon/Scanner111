using System.Threading.Tasks;
using Moq;
using Scanner111.CLI.Models;
using Scanner111.CLI.Services;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Xunit;

namespace Scanner111.Tests.CLI.Services;

[Collection("Settings Tests")]
public class CliSettingsServiceTests : IDisposable
{
    private readonly string _testSettingsDir;

    public CliSettingsServiceTests()
    {
        // Create a temporary directory for test settings
        _testSettingsDir = Path.Combine(Path.GetTempPath(), $"Scanner111Tests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testSettingsDir);
        
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
    public async Task LoadSettingsAsync_ReturnsApplicationSettings()
    {
        // Arrange
        var service = new CliSettingsService();
        
        // Act
        var settings = await service.LoadSettingsAsync();
        
        // Assert
        Assert.NotNull(settings);
        Assert.IsType<ApplicationSettings>(settings);
    }

    [Fact]
    public async Task SaveSettingsAsync_DoesNotThrow()
    {
        // Arrange
        var service = new CliSettingsService();
        var settings = new ApplicationSettings
        {
            FcxMode = true,
            ShowFormIdValues = true
        };
        
        // Act & Assert - Should not throw
        await service.SaveSettingsAsync(settings);
    }

    [Fact]
    public async Task SaveSettingAsync_UpdatesSingleSetting()
    {
        // Arrange
        var service = new CliSettingsService();
        
        // Act
        await service.SaveSettingAsync("FcxMode", true);
        
        // Assert
        var settings = await service.LoadSettingsAsync();
        Assert.True(settings.FcxMode);
    }

    [Fact]
    public void GetDefaultSettings_ReturnsValidDefaults()
    {
        // Arrange
        var service = new CliSettingsService();
        
        // Act
        var defaults = service.GetDefaultSettings();
        
        // Assert
        Assert.NotNull(defaults);
        Assert.False(defaults.FcxMode);
        Assert.False(defaults.ShowFormIdValues);
        Assert.True(defaults.CacheEnabled);
        Assert.Equal(Environment.ProcessorCount * 2, defaults.MaxConcurrentScans);
    }

    [Fact]
    public async Task LoadCliSettingsAsync_MapsFromApplicationSettings()
    {
        // Arrange
        var service = new CliSettingsService();
        
        // First save some app settings
        var appSettings = new ApplicationSettings
        {
            FcxMode = true,
            ShowFormIdValues = true,
            SimplifyLogs = true,
            MoveUnsolvedLogs = true,
            AudioNotifications = true,
            VrMode = true,
            DefaultScanDirectory = "C:\\Scans",
            DefaultGamePath = "C:\\Games",
            DefaultOutputFormat = "summary",
            DisableColors = true,
            DisableProgress = true,
            VerboseLogging = true,
            MaxConcurrentScans = 32,
            CacheEnabled = false,
            CrashLogsDirectory = "C:\\Crashes",
            MaxRecentItems = 20
        };
        appSettings.AddRecentScanDirectory("path1");
        appSettings.AddRecentScanDirectory("path2");
        
        await service.SaveSettingsAsync(appSettings);
        
        // Act
        var cliSettings = await service.LoadCliSettingsAsync();
        
        // Assert
        Assert.Equal(appSettings.FcxMode, cliSettings.FcxMode);
        Assert.Equal(appSettings.ShowFormIdValues, cliSettings.ShowFormIdValues);
        Assert.Equal(appSettings.SimplifyLogs, cliSettings.SimplifyLogs);
        Assert.Equal(appSettings.MoveUnsolvedLogs, cliSettings.MoveUnsolvedLogs);
        Assert.Equal(appSettings.AudioNotifications, cliSettings.AudioNotifications);
        Assert.Equal(appSettings.VrMode, cliSettings.VrMode);
        Assert.Equal(appSettings.DefaultScanDirectory, cliSettings.DefaultScanDirectory);
        Assert.Equal(appSettings.DefaultGamePath, cliSettings.DefaultGamePath);
        Assert.Equal(appSettings.DefaultOutputFormat, cliSettings.DefaultOutputFormat);
        Assert.Equal(appSettings.DisableColors, cliSettings.DisableColors);
        Assert.Equal(appSettings.DisableProgress, cliSettings.DisableProgress);
        Assert.Equal(appSettings.VerboseLogging, cliSettings.VerboseLogging);
        Assert.Equal(appSettings.MaxConcurrentScans, cliSettings.MaxConcurrentScans);
        Assert.Equal(appSettings.CacheEnabled, cliSettings.CacheEnabled);
        Assert.Equal(appSettings.CrashLogsDirectory, cliSettings.CrashLogsDirectory);
        Assert.Equal(appSettings.MaxRecentItems, cliSettings.MaxRecentPaths);
        Assert.Equal(2, cliSettings.RecentScanPaths.Count);
        Assert.Equal("path2", cliSettings.RecentScanPaths[0]);
        Assert.Equal("path1", cliSettings.RecentScanPaths[1]);
    }

    [Fact]
    public async Task SaveCliSettingsAsync_MapsToApplicationSettings()
    {
        // Arrange
        var service = new CliSettingsService();
        var cliSettings = new CliSettings
        {
            FcxMode = true,
            ShowFormIdValues = true,
            SimplifyLogs = true,
            MoveUnsolvedLogs = true,
            AudioNotifications = true,
            VrMode = true,
            DefaultScanDirectory = "C:\\CLI\\Scans",
            DefaultGamePath = "C:\\CLI\\Games",
            DefaultOutputFormat = "detailed",
            DisableColors = true,
            DisableProgress = true,
            VerboseLogging = true,
            MaxConcurrentScans = 8,
            CacheEnabled = false,
            CrashLogsDirectory = "C:\\CLI\\Crashes",
            MaxRecentPaths = 5
        };
        cliSettings.AddRecentPath("cli-path1");
        cliSettings.AddRecentPath("cli-path2");
        
        // Act
        await service.SaveCliSettingsAsync(cliSettings);
        
        // Assert - Load back as app settings
        var appSettings = await service.LoadSettingsAsync();
        Assert.Equal(cliSettings.FcxMode, appSettings.FcxMode);
        Assert.Equal(cliSettings.ShowFormIdValues, appSettings.ShowFormIdValues);
        Assert.Equal(cliSettings.SimplifyLogs, appSettings.SimplifyLogs);
        Assert.Equal(cliSettings.MoveUnsolvedLogs, appSettings.MoveUnsolvedLogs);
        Assert.Equal(cliSettings.AudioNotifications, appSettings.AudioNotifications);
        Assert.Equal(cliSettings.VrMode, appSettings.VrMode);
        Assert.Equal(cliSettings.DefaultScanDirectory, appSettings.DefaultScanDirectory);
        Assert.Equal(cliSettings.DefaultGamePath, appSettings.DefaultGamePath);
        Assert.Equal(cliSettings.DefaultOutputFormat, appSettings.DefaultOutputFormat);
        Assert.Equal(cliSettings.DisableColors, appSettings.DisableColors);
        Assert.Equal(cliSettings.DisableProgress, appSettings.DisableProgress);
        Assert.Equal(cliSettings.VerboseLogging, appSettings.VerboseLogging);
        Assert.Equal(cliSettings.MaxConcurrentScans, appSettings.MaxConcurrentScans);
        Assert.Equal(cliSettings.CacheEnabled, appSettings.CacheEnabled);
        Assert.Equal(cliSettings.CrashLogsDirectory, appSettings.CrashLogsDirectory);
        Assert.Equal(cliSettings.MaxRecentPaths, appSettings.MaxRecentItems);
        Assert.Equal(2, appSettings.RecentScanDirectories.Count);
        Assert.Equal("cli-path2", appSettings.RecentScanDirectories[0]);
        Assert.Equal("cli-path1", appSettings.RecentScanDirectories[1]);
    }

    [Fact]
    public async Task BackwardCompatibility_RoundTrip()
    {
        // Arrange
        var service = new CliSettingsService();
        var originalCli = new CliSettings
        {
            FcxMode = true,
            ShowFormIdValues = false,
            MaxConcurrentScans = 4,
            DefaultOutputFormat = "json"
        };
        originalCli.AddRecentPath("test-path");
        
        // Act - Save as CLI settings, load back as CLI settings
        await service.SaveCliSettingsAsync(originalCli);
        var loadedCli = await service.LoadCliSettingsAsync();
        
        // Assert
        Assert.Equal(originalCli.FcxMode, loadedCli.FcxMode);
        Assert.Equal(originalCli.ShowFormIdValues, loadedCli.ShowFormIdValues);
        Assert.Equal(originalCli.MaxConcurrentScans, loadedCli.MaxConcurrentScans);
        Assert.Equal(originalCli.DefaultOutputFormat, loadedCli.DefaultOutputFormat);
        Assert.Single(loadedCli.RecentScanPaths);
        Assert.Equal("test-path", loadedCli.RecentScanPaths[0]);
    }

    [Fact]
    public async Task MappingPreservesUnmappedApplicationSettings()
    {
        // Arrange
        var service = new CliSettingsService();
        
        // Set up app settings with values that don't map to CLI settings
        var appSettings = await service.LoadSettingsAsync();
        appSettings.WindowWidth = 1920;
        appSettings.WindowHeight = 1080;
        appSettings.RememberWindowSize = false;
        appSettings.RecentLogFiles.Add("log1.txt");
        appSettings.RecentGamePaths.Add("game1");
        await service.SaveSettingsAsync(appSettings);
        
        // Act - Save CLI settings
        var cliSettings = new CliSettings { FcxMode = true };
        await service.SaveCliSettingsAsync(cliSettings);
        
        // Assert - Unmapped values should be preserved
        var reloadedApp = await service.LoadSettingsAsync();
        Assert.Equal(1920, reloadedApp.WindowWidth);
        Assert.Equal(1080, reloadedApp.WindowHeight);
        Assert.False(reloadedApp.RememberWindowSize);
        Assert.Contains("log1.txt", reloadedApp.RecentLogFiles);
        Assert.Contains("game1", reloadedApp.RecentGamePaths);
        Assert.True(reloadedApp.FcxMode); // CLI setting was applied
    }

    [Fact]
    public async Task CliSettingsService_HandlesNullRecentPaths()
    {
        // Arrange
        var service = new CliSettingsService();
        var cliSettings = new CliSettings
        {
            RecentScanPaths = null // Simulate null list
        };
        
        // Act & Assert - Should handle gracefully
        await service.SaveCliSettingsAsync(cliSettings);
        var loaded = await service.LoadCliSettingsAsync();
        
        Assert.NotNull(loaded.RecentScanPaths);
    }

    [Fact]
    public async Task SaveSettingAsync_HandlesVariousTypes()
    {
        // Arrange
        var service = new CliSettingsService();
        
        // Act & Assert - Should handle different types
        await service.SaveSettingAsync("FcxMode", true);
        await service.SaveSettingAsync("MaxConcurrentScans", 24);
        await service.SaveSettingAsync("DefaultOutputFormat", "xml");
        
        var settings = await service.LoadSettingsAsync();
        Assert.True(settings.FcxMode);
        Assert.Equal(24, settings.MaxConcurrentScans);
        Assert.Equal("xml", settings.DefaultOutputFormat);
    }
}