using System.Linq;
using System.Text.Json;
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
        Assert.False(settings.FcxMode);
        Assert.False(settings.ShowFormIdValues);
        Assert.False(settings.SimplifyLogs);
        Assert.False(settings.MoveUnsolvedLogs);
        Assert.False(settings.AudioNotifications);
        Assert.False(settings.VrMode);
        Assert.Equal("", settings.DefaultScanDirectory);
        Assert.Equal("", settings.DefaultGamePath);
        Assert.Equal("detailed", settings.DefaultOutputFormat);
        Assert.False(settings.DisableColors);
        Assert.False(settings.DisableProgress);
        Assert.False(settings.VerboseLogging);
        Assert.Equal(16, settings.MaxConcurrentScans);
        Assert.True(settings.CacheEnabled);
        Assert.NotNull(settings.RecentScanPaths);
        Assert.Empty(settings.RecentScanPaths);
        Assert.Equal(10, settings.MaxRecentPaths);
        Assert.Equal("", settings.CrashLogsDirectory);
        Assert.Equal("", settings.GamePath);
        Assert.Equal("", settings.ModsFolder);
        Assert.Equal("", settings.IniFolder);
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
        Assert.Equal(2, settings.RecentScanPaths.Count);
        Assert.Equal("path2", settings.RecentScanPaths[0]);
        Assert.Equal("path1", settings.RecentScanPaths[1]);
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
        Assert.Equal(2, settings.RecentScanPaths.Count);
        Assert.Equal("path1", settings.RecentScanPaths[0]); // path1 should be at front
        Assert.Equal("path2", settings.RecentScanPaths[1]);
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
        Assert.Equal(3, settings.RecentScanPaths.Count);
        Assert.Equal("path4", settings.RecentScanPaths[0]);
        Assert.Equal("path3", settings.RecentScanPaths[1]);
        Assert.Equal("path2", settings.RecentScanPaths[2]);
        Assert.DoesNotContain("path1", settings.RecentScanPaths);
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
        Assert.Empty(settings.RecentScanPaths);
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
        Assert.NotNull(deserialized);
        Assert.Equal(settings.FcxMode, deserialized.FcxMode);
        Assert.Equal(settings.ShowFormIdValues, deserialized.ShowFormIdValues);
        Assert.Equal(settings.SimplifyLogs, deserialized.SimplifyLogs);
        Assert.Equal(settings.MoveUnsolvedLogs, deserialized.MoveUnsolvedLogs);
        Assert.Equal(settings.AudioNotifications, deserialized.AudioNotifications);
        Assert.Equal(settings.VrMode, deserialized.VrMode);
        Assert.Equal(settings.DefaultScanDirectory, deserialized.DefaultScanDirectory);
        Assert.Equal(settings.DefaultGamePath, deserialized.DefaultGamePath);
        Assert.Equal(settings.DefaultOutputFormat, deserialized.DefaultOutputFormat);
        Assert.Equal(settings.DisableColors, deserialized.DisableColors);
        Assert.Equal(settings.DisableProgress, deserialized.DisableProgress);
        Assert.Equal(settings.VerboseLogging, deserialized.VerboseLogging);
        Assert.Equal(settings.MaxConcurrentScans, deserialized.MaxConcurrentScans);
        Assert.Equal(settings.CacheEnabled, deserialized.CacheEnabled);
        Assert.Equal(settings.MaxRecentPaths, deserialized.MaxRecentPaths);
        Assert.Equal(settings.CrashLogsDirectory, deserialized.CrashLogsDirectory);
        Assert.Equal(settings.GamePath, deserialized.GamePath);
        Assert.Equal(settings.ModsFolder, deserialized.ModsFolder);
        Assert.Equal(settings.IniFolder, deserialized.IniFolder);
        Assert.Equal(2, deserialized.RecentScanPaths.Count);
        Assert.Equal("path2", deserialized.RecentScanPaths[0]);
        Assert.Equal("path1", deserialized.RecentScanPaths[1]);
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
        Assert.Contains("\"fcxMode\":true", json);
        Assert.Contains("\"showFormIdValues\":true", json);
        Assert.Contains("\"defaultOutputFormat\":\"detailed\"", json);
        Assert.Contains("\"maxConcurrentScans\":16", json);
    }

    [Fact]
    public void AllPropertiesCanBeModified()
    {
        // Arrange
        var settings = new CliSettings();
        
        // Act & Assert
        settings.FcxMode = true;
        Assert.True(settings.FcxMode);
        
        settings.ShowFormIdValues = true;
        Assert.True(settings.ShowFormIdValues);
        
        settings.SimplifyLogs = true;
        Assert.True(settings.SimplifyLogs);
        
        settings.MoveUnsolvedLogs = true;
        Assert.True(settings.MoveUnsolvedLogs);
        
        settings.AudioNotifications = true;
        Assert.True(settings.AudioNotifications);
        
        settings.VrMode = true;
        Assert.True(settings.VrMode);
        
        settings.DefaultScanDirectory = "test";
        Assert.Equal("test", settings.DefaultScanDirectory);
        
        settings.DefaultGamePath = "gamepath";
        Assert.Equal("gamepath", settings.DefaultGamePath);
        
        settings.DefaultOutputFormat = "json";
        Assert.Equal("json", settings.DefaultOutputFormat);
        
        settings.DisableColors = true;
        Assert.True(settings.DisableColors);
        
        settings.DisableProgress = true;
        Assert.True(settings.DisableProgress);
        
        settings.VerboseLogging = true;
        Assert.True(settings.VerboseLogging);
        
        settings.MaxConcurrentScans = 8;
        Assert.Equal(8, settings.MaxConcurrentScans);
        
        settings.CacheEnabled = false;
        Assert.False(settings.CacheEnabled);
        
        settings.MaxRecentPaths = 5;
        Assert.Equal(5, settings.MaxRecentPaths);
        
        settings.CrashLogsDirectory = "crashes";
        Assert.Equal("crashes", settings.CrashLogsDirectory);
        
        settings.GamePath = "game";
        Assert.Equal("game", settings.GamePath);
        
        settings.ModsFolder = "mods";
        Assert.Equal("mods", settings.ModsFolder);
        
        settings.IniFolder = "ini";
        Assert.Equal("ini", settings.IniFolder);
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
        Assert.Single(settings.RecentScanPaths);
        Assert.Equal(longPath, settings.RecentScanPaths[0]);
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
        Assert.Equal(5, settings.RecentScanPaths.Count);
        Assert.Equal("path10", settings.RecentScanPaths[0]);
        Assert.Equal("path9", settings.RecentScanPaths[1]);
        Assert.Equal("path8", settings.RecentScanPaths[2]);
        Assert.Equal("path7", settings.RecentScanPaths[3]);
        Assert.Equal("path6", settings.RecentScanPaths[4]);
    }
}