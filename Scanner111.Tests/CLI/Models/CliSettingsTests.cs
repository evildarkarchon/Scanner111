using System.Linq;
using System.Text.Json;
using FluentAssertions;
using Scanner111.CLI.Models;
using Xunit;

namespace Scanner111.Tests.CLI.Models;

public class CliSettingsTests
{
    [Fact]
    public void Constructor_InitializesDefaultValues()
    {
        // Arrange & Act
        var settings = new CliSettings();
        
        // Assert
        settings.FcxMode.Should().BeFalse("because FCX mode is disabled by default");
        settings.ShowFormIdValues.Should().BeFalse("because FormID values are hidden by default");
        settings.SimplifyLogs.Should().BeFalse("because log simplification is disabled by default");
        settings.MoveUnsolvedLogs.Should().BeFalse("because moving unsolved logs is disabled by default");
        settings.AudioNotifications.Should().BeFalse("because audio notifications are disabled by default");
        settings.VrMode.Should().BeFalse("because VR mode is disabled by default");
        settings.DefaultScanDirectory.Should().Be("", "because no default scan directory is set");
        settings.DefaultGamePath.Should().Be("", "because no default game path is set");
        settings.DefaultOutputFormat.Should().Be("detailed", "because detailed is the default output format");
        settings.DisableColors.Should().BeFalse("because colors are enabled by default");
        settings.DisableProgress.Should().BeFalse("because progress is enabled by default");
        settings.VerboseLogging.Should().BeFalse("because verbose logging is disabled by default");
        settings.MaxConcurrentScans.Should().Be(16, "because 16 is the default concurrency limit");
        settings.CacheEnabled.Should().BeTrue("because cache is enabled by default");
        settings.RecentScanPaths.Should().NotBeNull("because the list should be initialized");
        settings.RecentScanPaths.Should().BeEmpty("because no recent paths exist initially");
        settings.MaxRecentPaths.Should().Be(10, "because 10 is the default max recent paths");
        settings.CrashLogsDirectory.Should().Be("", "because no crash logs directory is set by default");
        settings.GamePath.Should().Be("", "because no game path is set by default");
        settings.ModsFolder.Should().Be("", "because no mods folder is set by default");
        settings.IniFolder.Should().Be("", "because no ini folder is set by default");
    }

    [Fact]
    public void AddRecentPath_AddsPathToFrontOfList()
    {
        // Arrange
        var settings = new CliSettings();
        
        // Act
        settings.AddRecentPath("path1");
        settings.AddRecentPath("path2");
        
        // Assert
        settings.RecentScanPaths.Should().HaveCount(2, "because two paths were added");
        settings.RecentScanPaths[0].Should().Be("path2", "because most recent path should be first");
        settings.RecentScanPaths[1].Should().Be("path1", "because older path should be second");
    }

    [Fact]
    public void AddRecentPath_RemovesDuplicates()
    {
        // Arrange
        var settings = new CliSettings();
        
        // Act
        settings.AddRecentPath("path1");
        settings.AddRecentPath("path2");
        settings.AddRecentPath("path1"); // Add path1 again
        
        // Assert
        settings.RecentScanPaths.Should().HaveCount(2, "because duplicates should be removed");
        settings.RecentScanPaths[0].Should().Be("path1", "because re-added path should move to front");
        settings.RecentScanPaths[1].Should().Be("path2", "because older path should be second");
    }

    [Fact]
    public void AddRecentPath_RespectsMaxRecentPaths()
    {
        // Arrange
        var settings = new CliSettings { MaxRecentPaths = 3 };
        
        // Act
        settings.AddRecentPath("path1");
        settings.AddRecentPath("path2");
        settings.AddRecentPath("path3");
        settings.AddRecentPath("path4");
        
        // Assert
        settings.RecentScanPaths.Should().HaveCount(3, "because max limit is 3");
        settings.RecentScanPaths[0].Should().Be("path4", "because it's the most recent");
        settings.RecentScanPaths[1].Should().Be("path3", "because it's second most recent");
        settings.RecentScanPaths[2].Should().Be("path2", "because it's third most recent");
        settings.RecentScanPaths.Should().NotContain("path1", "because it was removed when limit was reached");
    }

    [Fact]
    public void AddRecentPath_IgnoresEmptyOrNullPaths()
    {
        // Arrange
        var settings = new CliSettings();
        
        // Act
        settings.AddRecentPath(null);
        settings.AddRecentPath("");
        settings.AddRecentPath("   ");
        
        // Assert
        settings.RecentScanPaths.Should().BeEmpty("because empty/null paths should be ignored");
    }

    [Fact]
    public void JsonSerialization_PreservesAllProperties()
    {
        // Arrange
        var settings = new CliSettings
        {
            FcxMode = true,
            ShowFormIdValues = true,
            SimplifyLogs = true,
            MoveUnsolvedLogs = true,
            AudioNotifications = true,
            VrMode = true,
            DefaultScanDirectory = "C:\\Scans",
            DefaultGamePath = "C:\\Games\\Fallout4",
            DefaultOutputFormat = "summary",
            DisableColors = true,
            DisableProgress = true,
            VerboseLogging = true,
            MaxConcurrentScans = 32,
            CacheEnabled = false,
            MaxRecentPaths = 20,
            CrashLogsDirectory = "C:\\CrashLogs",
            GamePath = "C:\\Games\\Fallout4",
            ModsFolder = "C:\\Mods",
            IniFolder = "C:\\Ini"
        };
        settings.AddRecentPath("path1");
        settings.AddRecentPath("path2");
        
        // Act
        var json = JsonSerializer.Serialize(settings);
        var deserialized = JsonSerializer.Deserialize<CliSettings>(json);
        
        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.FcxMode.Should().Be(settings.FcxMode);
        deserialized.ShowFormIdValues.Should().Be(settings.ShowFormIdValues);
        deserialized.SimplifyLogs.Should().Be(settings.SimplifyLogs);
        deserialized.MoveUnsolvedLogs.Should().Be(settings.MoveUnsolvedLogs);
        deserialized.AudioNotifications.Should().Be(settings.AudioNotifications);
        deserialized.VrMode.Should().Be(settings.VrMode);
        deserialized.DefaultScanDirectory.Should().Be(settings.DefaultScanDirectory);
        deserialized.DefaultGamePath.Should().Be(settings.DefaultGamePath);
        deserialized.DefaultOutputFormat.Should().Be(settings.DefaultOutputFormat);
        deserialized.DisableColors.Should().Be(settings.DisableColors);
        deserialized.DisableProgress.Should().Be(settings.DisableProgress);
        deserialized.VerboseLogging.Should().Be(settings.VerboseLogging);
        deserialized.MaxConcurrentScans.Should().Be(settings.MaxConcurrentScans);
        deserialized.CacheEnabled.Should().Be(settings.CacheEnabled);
        deserialized.MaxRecentPaths.Should().Be(settings.MaxRecentPaths);
        deserialized.CrashLogsDirectory.Should().Be(settings.CrashLogsDirectory);
        deserialized.GamePath.Should().Be(settings.GamePath);
        deserialized.ModsFolder.Should().Be(settings.ModsFolder);
        deserialized.IniFolder.Should().Be(settings.IniFolder);
        deserialized.RecentScanPaths.Should().HaveCount(2);
        deserialized.RecentScanPaths[0].Should().Be("path2");
        deserialized.RecentScanPaths[1].Should().Be("path1");
    }

    [Fact]
    public void JsonPropertyNames_AreCorrect()
    {
        // Arrange
        var settings = new CliSettings
        {
            FcxMode = true,
            ShowFormIdValues = true
        };
        
        // Act
        var json = JsonSerializer.Serialize(settings);
        
        // Assert
        json.Should().Contain("\"fcxMode\":true");
        json.Should().Contain("\"showFormIdValues\":true");
        json.Should().Contain("\"defaultOutputFormat\":\"detailed\"");
        json.Should().Contain("\"maxConcurrentScans\":16");
    }

    [Fact]
    public void AllPropertiesCanBeModified()
    {
        // Arrange
        var settings = new CliSettings();
        
        // Act & Assert
        settings.FcxMode = true;
        settings.FcxMode.Should().BeTrue();
        
        settings.ShowFormIdValues = true;
        settings.ShowFormIdValues.Should().BeTrue();
        
        settings.SimplifyLogs = true;
        settings.SimplifyLogs.Should().BeTrue();
        
        settings.MoveUnsolvedLogs = true;
        settings.MoveUnsolvedLogs.Should().BeTrue();
        
        settings.AudioNotifications = true;
        settings.AudioNotifications.Should().BeTrue();
        
        settings.VrMode = true;
        settings.VrMode.Should().BeTrue();
        
        settings.DefaultScanDirectory = "test";
        settings.DefaultScanDirectory.Should().Be("test");
        
        settings.DefaultGamePath = "gamepath";
        settings.DefaultGamePath.Should().Be("gamepath");
        
        settings.DefaultOutputFormat = "json";
        settings.DefaultOutputFormat.Should().Be("json");
        
        settings.DisableColors = true;
        settings.DisableColors.Should().BeTrue();
        
        settings.DisableProgress = true;
        settings.DisableProgress.Should().BeTrue();
        
        settings.VerboseLogging = true;
        settings.VerboseLogging.Should().BeTrue();
        
        settings.MaxConcurrentScans = 8;
        settings.MaxConcurrentScans.Should().Be(8);
        
        settings.CacheEnabled = false;
        settings.CacheEnabled.Should().BeFalse();
        
        settings.MaxRecentPaths = 5;
        settings.MaxRecentPaths.Should().Be(5);
        
        settings.CrashLogsDirectory = "crashes";
        settings.CrashLogsDirectory.Should().Be("crashes");
        
        settings.GamePath = "game";
        settings.GamePath.Should().Be("game");
        
        settings.ModsFolder = "mods";
        settings.ModsFolder.Should().Be("mods");
        
        settings.IniFolder = "ini";
        settings.IniFolder.Should().Be("ini");
    }

    [Fact]
    public void AddRecentPath_HandlesLongPaths()
    {
        // Arrange
        var settings = new CliSettings();
        var longPath = string.Join("\\", Enumerable.Repeat("verylongfoldername", 20));
        
        // Act
        settings.AddRecentPath(longPath);
        
        // Assert
        settings.RecentScanPaths.Should().ContainSingle();
        settings.RecentScanPaths[0].Should().Be(longPath);
    }

    [Fact]
    public void AddRecentPath_MaintainsOrderWithMaxLimit()
    {
        // Arrange
        var settings = new CliSettings { MaxRecentPaths = 5 };
        
        // Act - Add more than max
        for (int i = 1; i <= 10; i++)
        {
            settings.AddRecentPath($"path{i}");
        }
        
        // Assert - Should have only the last 5, in reverse order
        settings.RecentScanPaths.Should().HaveCount(5);
        settings.RecentScanPaths[0].Should().Be("path10");
        settings.RecentScanPaths[1].Should().Be("path9");
        settings.RecentScanPaths[2].Should().Be("path8");
        settings.RecentScanPaths[3].Should().Be("path7");
        settings.RecentScanPaths[4].Should().Be("path6");
    }
}