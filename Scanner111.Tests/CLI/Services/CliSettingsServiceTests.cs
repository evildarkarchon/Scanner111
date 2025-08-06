using System.Threading.Tasks;
using FluentAssertions;
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
        settings.Should().NotBeNull("because LoadSettingsAsync should return settings");
        settings.Should().BeOfType<ApplicationSettings>("because the service returns ApplicationSettings");
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
        settings.FcxMode.Should().BeTrue("because FcxMode was set to true");
    }

    [Fact]
    public void GetDefaultSettings_ReturnsValidDefaults()
    {
        // Arrange
        var service = new CliSettingsService();
        
        // Act
        var defaults = service.GetDefaultSettings();
        
        // Assert
        defaults.Should().NotBeNull("because GetDefaultSettings should return valid defaults");
        defaults.FcxMode.Should().BeFalse("because FcxMode is false by default");
        defaults.ShowFormIdValues.Should().BeFalse("because ShowFormIdValues is false by default");
        defaults.CacheEnabled.Should().BeTrue("because CacheEnabled is true by default");
        defaults.MaxConcurrentScans.Should().Be(Environment.ProcessorCount * 2, "because default is 2x processor count");
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
        cliSettings.FcxMode.Should().Be(appSettings.FcxMode, "because settings should map correctly");
        cliSettings.ShowFormIdValues.Should().Be(appSettings.ShowFormIdValues);
        cliSettings.SimplifyLogs.Should().Be(appSettings.SimplifyLogs);
        cliSettings.MoveUnsolvedLogs.Should().Be(appSettings.MoveUnsolvedLogs);
        cliSettings.AudioNotifications.Should().Be(appSettings.AudioNotifications);
        cliSettings.VrMode.Should().Be(appSettings.VrMode);
        cliSettings.DefaultScanDirectory.Should().Be(appSettings.DefaultScanDirectory);
        cliSettings.DefaultGamePath.Should().Be(appSettings.DefaultGamePath);
        cliSettings.DefaultOutputFormat.Should().Be(appSettings.DefaultOutputFormat);
        cliSettings.DisableColors.Should().Be(appSettings.DisableColors);
        cliSettings.DisableProgress.Should().Be(appSettings.DisableProgress);
        cliSettings.VerboseLogging.Should().Be(appSettings.VerboseLogging);
        cliSettings.MaxConcurrentScans.Should().Be(appSettings.MaxConcurrentScans);
        cliSettings.CacheEnabled.Should().Be(appSettings.CacheEnabled);
        cliSettings.CrashLogsDirectory.Should().Be(appSettings.CrashLogsDirectory);
        cliSettings.MaxRecentPaths.Should().Be(appSettings.MaxRecentItems);
        cliSettings.RecentScanPaths.Should().HaveCount(2, "because two paths were added");
        cliSettings.RecentScanPaths[0].Should().Be("path2", "because most recent path is first");
        cliSettings.RecentScanPaths[1].Should().Be("path1");
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
        appSettings.FcxMode.Should().Be(cliSettings.FcxMode, "because settings should map correctly");
        appSettings.ShowFormIdValues.Should().Be(cliSettings.ShowFormIdValues);
        appSettings.SimplifyLogs.Should().Be(cliSettings.SimplifyLogs);
        appSettings.MoveUnsolvedLogs.Should().Be(cliSettings.MoveUnsolvedLogs);
        appSettings.AudioNotifications.Should().Be(cliSettings.AudioNotifications);
        appSettings.VrMode.Should().Be(cliSettings.VrMode);
        appSettings.DefaultScanDirectory.Should().Be(cliSettings.DefaultScanDirectory);
        appSettings.DefaultGamePath.Should().Be(cliSettings.DefaultGamePath);
        appSettings.DefaultOutputFormat.Should().Be(cliSettings.DefaultOutputFormat);
        appSettings.DisableColors.Should().Be(cliSettings.DisableColors);
        appSettings.DisableProgress.Should().Be(cliSettings.DisableProgress);
        appSettings.VerboseLogging.Should().Be(cliSettings.VerboseLogging);
        appSettings.MaxConcurrentScans.Should().Be(cliSettings.MaxConcurrentScans);
        appSettings.CacheEnabled.Should().Be(cliSettings.CacheEnabled);
        appSettings.CrashLogsDirectory.Should().Be(cliSettings.CrashLogsDirectory);
        appSettings.MaxRecentItems.Should().Be(cliSettings.MaxRecentPaths);
        appSettings.RecentScanDirectories.Should().HaveCount(2, "because two paths were added");
        appSettings.RecentScanDirectories[0].Should().Be("cli-path2", "because most recent path is first");
        appSettings.RecentScanDirectories[1].Should().Be("cli-path1");
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
        loadedCli.FcxMode.Should().Be(originalCli.FcxMode, "because settings should round-trip correctly");
        loadedCli.ShowFormIdValues.Should().Be(originalCli.ShowFormIdValues);
        loadedCli.MaxConcurrentScans.Should().Be(originalCli.MaxConcurrentScans);
        loadedCli.DefaultOutputFormat.Should().Be(originalCli.DefaultOutputFormat);
        loadedCli.RecentScanPaths.Should().ContainSingle("because one path was added");
        loadedCli.RecentScanPaths[0].Should().Be("test-path");
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
        reloadedApp.WindowWidth.Should().Be(1920, "because window width should be preserved");
        reloadedApp.WindowHeight.Should().Be(1080, "because window height should be preserved");
        reloadedApp.RememberWindowSize.Should().BeFalse("because RememberWindowSize should be preserved");
        reloadedApp.RecentLogFiles.Should().Contain("log1.txt", "because recent log files should be preserved");
        reloadedApp.RecentGamePaths.Should().Contain("game1", "because recent game paths should be preserved");
        reloadedApp.FcxMode.Should().BeTrue("because CLI setting was applied");
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
        
        loaded.RecentScanPaths.Should().NotBeNull("because null paths should be handled gracefully");
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
        settings.FcxMode.Should().BeTrue("because FcxMode was set to true");
        settings.MaxConcurrentScans.Should().Be(24, "because MaxConcurrentScans was set to 24");
        settings.DefaultOutputFormat.Should().Be("xml", "because DefaultOutputFormat was set to xml");
    }
}