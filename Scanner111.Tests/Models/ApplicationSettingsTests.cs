using System.Text.Json.Serialization;
using Scanner111.Core.Models;
using Xunit;

namespace Scanner111.Tests.Models;

public class ApplicationSettingsTests
{
    [Fact]
    public void Constructor_InitializesDefaultValues()
    {
        var settings = new ApplicationSettings();
        
        Assert.False(settings.FcxMode);
        Assert.False(settings.ShowFormIdValues);
        Assert.False(settings.SimplifyLogs);
        Assert.False(settings.MoveUnsolvedLogs);
        Assert.False(settings.VrMode);
        Assert.Equal("", settings.DefaultLogPath);
        Assert.Equal("", settings.DefaultGamePath);
        Assert.Equal("", settings.GamePath);
        Assert.Equal("", settings.DefaultScanDirectory);
        Assert.Equal("", settings.CrashLogsDirectory);
        Assert.Equal("", settings.BackupDirectory);
        Assert.Equal("", settings.ModsFolder);
        Assert.Equal("", settings.IniFolder);
        Assert.Equal("text", settings.DefaultOutputFormat);
        Assert.True(settings.AutoSaveResults);
        Assert.True(settings.AutoLoadF4SeLogs);
        Assert.False(settings.SkipXseCopy);
        Assert.Equal(16, settings.MaxConcurrentScans);
        Assert.True(settings.CacheEnabled);
        Assert.False(settings.EnableDebugLogging);
        Assert.False(settings.VerboseLogging);
        Assert.False(settings.AudioNotifications);
        Assert.True(settings.EnableProgressNotifications);
        Assert.True(settings.EnableUpdateCheck);
        Assert.Equal("Both", settings.UpdateSource);
        Assert.False(settings.DisableColors);
        Assert.False(settings.DisableProgress);
        Assert.True(settings.RememberWindowSize);
        Assert.Equal(1200, settings.WindowWidth);
        Assert.Equal(800, settings.WindowHeight);
        Assert.Equal(100, settings.MaxLogMessages);
        Assert.NotNull(settings.RecentLogFiles);
        Assert.Empty(settings.RecentLogFiles);
        Assert.NotNull(settings.RecentGamePaths);
        Assert.Empty(settings.RecentGamePaths);
        Assert.NotNull(settings.RecentScanDirectories);
        Assert.Empty(settings.RecentScanDirectories);
        Assert.Equal(10, settings.MaxRecentItems);
        Assert.NotNull(settings.LastUsedAnalyzers);
        Assert.Empty(settings.LastUsedAnalyzers);
    }

    [Fact]
    public void RecentScanPaths_ReturnsRecentScanDirectories()
    {
        var settings = new ApplicationSettings();
        settings.RecentScanDirectories.Add("path1");
        settings.RecentScanDirectories.Add("path2");
        
        Assert.Same(settings.RecentScanDirectories, settings.RecentScanPaths);
        Assert.Equal(2, settings.RecentScanPaths.Count);
        Assert.Equal("path1", settings.RecentScanPaths[0]);
        Assert.Equal("path2", settings.RecentScanPaths[1]);
    }

    [Fact]
    public void AddRecentLogFile_AddsToFrontOfList()
    {
        var settings = new ApplicationSettings();
        
        settings.AddRecentLogFile("path1");
        settings.AddRecentLogFile("path2");
        
        Assert.Equal(2, settings.RecentLogFiles.Count);
        Assert.Equal("path2", settings.RecentLogFiles[0]);
        Assert.Equal("path1", settings.RecentLogFiles[1]);
    }

    [Fact]
    public void AddRecentLogFile_RemovesDuplicates()
    {
        var settings = new ApplicationSettings();
        
        settings.AddRecentLogFile("path1");
        settings.AddRecentLogFile("path2");
        settings.AddRecentLogFile("path1");
        
        Assert.Equal(2, settings.RecentLogFiles.Count);
        Assert.Equal("path1", settings.RecentLogFiles[0]);
        Assert.Equal("path2", settings.RecentLogFiles[1]);
    }

    [Fact]
    public void AddRecentLogFile_RespectsMaxRecentItems()
    {
        var settings = new ApplicationSettings { MaxRecentItems = 3 };
        
        settings.AddRecentLogFile("path1");
        settings.AddRecentLogFile("path2");
        settings.AddRecentLogFile("path3");
        settings.AddRecentLogFile("path4");
        
        Assert.Equal(3, settings.RecentLogFiles.Count);
        Assert.Equal("path4", settings.RecentLogFiles[0]);
        Assert.Equal("path3", settings.RecentLogFiles[1]);
        Assert.Equal("path2", settings.RecentLogFiles[2]);
        Assert.DoesNotContain("path1", settings.RecentLogFiles);
    }

    [Fact]
    public void AddRecentLogFile_IgnoresEmptyOrNullPaths()
    {
        var settings = new ApplicationSettings();
        
        settings.AddRecentLogFile(null);
        settings.AddRecentLogFile("");
        settings.AddRecentLogFile("   ");
        
        Assert.Empty(settings.RecentLogFiles);
    }

    [Fact]
    public void AddRecentGamePath_AddsToFrontOfList()
    {
        var settings = new ApplicationSettings();
        
        settings.AddRecentGamePath("path1");
        settings.AddRecentGamePath("path2");
        
        Assert.Equal(2, settings.RecentGamePaths.Count);
        Assert.Equal("path2", settings.RecentGamePaths[0]);
        Assert.Equal("path1", settings.RecentGamePaths[1]);
    }

    [Fact]
    public void AddRecentGamePath_RemovesDuplicates()
    {
        var settings = new ApplicationSettings();
        
        settings.AddRecentGamePath("path1");
        settings.AddRecentGamePath("path2");
        settings.AddRecentGamePath("path1");
        
        Assert.Equal(2, settings.RecentGamePaths.Count);
        Assert.Equal("path1", settings.RecentGamePaths[0]);
        Assert.Equal("path2", settings.RecentGamePaths[1]);
    }

    [Fact]
    public void AddRecentGamePath_RespectsMaxRecentItems()
    {
        var settings = new ApplicationSettings { MaxRecentItems = 3 };
        
        settings.AddRecentGamePath("path1");
        settings.AddRecentGamePath("path2");
        settings.AddRecentGamePath("path3");
        settings.AddRecentGamePath("path4");
        
        Assert.Equal(3, settings.RecentGamePaths.Count);
        Assert.Equal("path4", settings.RecentGamePaths[0]);
        Assert.Equal("path3", settings.RecentGamePaths[1]);
        Assert.Equal("path2", settings.RecentGamePaths[2]);
        Assert.DoesNotContain("path1", settings.RecentGamePaths);
    }

    [Fact]
    public void AddRecentGamePath_IgnoresEmptyOrNullPaths()
    {
        var settings = new ApplicationSettings();
        
        settings.AddRecentGamePath(null);
        settings.AddRecentGamePath("");
        
        Assert.Empty(settings.RecentGamePaths);
    }

    [Fact]
    public void AddRecentScanDirectory_AddsToFrontOfList()
    {
        var settings = new ApplicationSettings();
        
        settings.AddRecentScanDirectory("path1");
        settings.AddRecentScanDirectory("path2");
        
        Assert.Equal(2, settings.RecentScanDirectories.Count);
        Assert.Equal("path2", settings.RecentScanDirectories[0]);
        Assert.Equal("path1", settings.RecentScanDirectories[1]);
    }

    [Fact]
    public void AddRecentScanDirectory_RemovesDuplicates()
    {
        var settings = new ApplicationSettings();
        
        settings.AddRecentScanDirectory("path1");
        settings.AddRecentScanDirectory("path2");
        settings.AddRecentScanDirectory("path1");
        
        Assert.Equal(2, settings.RecentScanDirectories.Count);
        Assert.Equal("path1", settings.RecentScanDirectories[0]);
        Assert.Equal("path2", settings.RecentScanDirectories[1]);
    }

    [Fact]
    public void AddRecentScanDirectory_RespectsMaxRecentItems()
    {
        var settings = new ApplicationSettings { MaxRecentItems = 3 };
        
        settings.AddRecentScanDirectory("path1");
        settings.AddRecentScanDirectory("path2");
        settings.AddRecentScanDirectory("path3");
        settings.AddRecentScanDirectory("path4");
        
        Assert.Equal(3, settings.RecentScanDirectories.Count);
        Assert.Equal("path4", settings.RecentScanDirectories[0]);
        Assert.Equal("path3", settings.RecentScanDirectories[1]);
        Assert.Equal("path2", settings.RecentScanDirectories[2]);
        Assert.DoesNotContain("path1", settings.RecentScanDirectories);
    }

    [Fact]
    public void AddRecentScanDirectory_IgnoresEmptyOrNullPaths()
    {
        var settings = new ApplicationSettings();
        
        settings.AddRecentScanDirectory(null);
        settings.AddRecentScanDirectory("");
        
        Assert.Empty(settings.RecentScanDirectories);
    }

    [Fact]
    public void AddRecentPath_DelegatesToAddRecentScanDirectory()
    {
        var settings = new ApplicationSettings();
        
        settings.AddRecentPath("path1");
        settings.AddRecentPath("path2");
        
        Assert.Equal(2, settings.RecentScanDirectories.Count);
        Assert.Equal("path2", settings.RecentScanDirectories[0]);
        Assert.Equal("path1", settings.RecentScanDirectories[1]);
    }

    [Fact]
    public void RecentItemsManagement_IsThreadSafe()
    {
        var settings = new ApplicationSettings { MaxRecentItems = 10 };
        var tasks = new List<Task>();
        
        // Add items concurrently from multiple threads
        for (int i = 0; i < 100; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                settings.AddRecentLogFile($"log{index}");
                settings.AddRecentGamePath($"game{index}");
                settings.AddRecentScanDirectory($"scan{index}");
            }));
        }
        
        Task.WaitAll(tasks.ToArray());
        
        // Verify lists are not corrupted and respect max items
        Assert.True(settings.RecentLogFiles.Count <= settings.MaxRecentItems);
        Assert.True(settings.RecentGamePaths.Count <= settings.MaxRecentItems);
        Assert.True(settings.RecentScanDirectories.Count <= settings.MaxRecentItems);
        
        // Verify no null entries were added during concurrent access
        Assert.DoesNotContain(null, settings.RecentLogFiles);
        Assert.DoesNotContain(null, settings.RecentGamePaths);
        Assert.DoesNotContain(null, settings.RecentScanDirectories);
    }

    [Fact]
    public void JsonPropertyNames_AreSetCorrectly()
    {
        var settings = new ApplicationSettings();
        var type = settings.GetType();
        
        // Verify some key properties have correct JSON property names
        var fcxModeProperty = type.GetProperty(nameof(ApplicationSettings.FcxMode));
        var fcxModeAttribute = fcxModeProperty?.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false)
            .FirstOrDefault() as JsonPropertyNameAttribute;
        Assert.Equal("fcxMode", fcxModeAttribute?.Name);
        
        var showFormIdProperty = type.GetProperty(nameof(ApplicationSettings.ShowFormIdValues));
        var showFormIdAttribute = showFormIdProperty?.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false)
            .FirstOrDefault() as JsonPropertyNameAttribute;
        Assert.Equal("showFormIdValues", showFormIdAttribute?.Name);
        
        var defaultLogPathProperty = type.GetProperty(nameof(ApplicationSettings.DefaultLogPath));
        var defaultLogPathAttribute = defaultLogPathProperty?.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false)
            .FirstOrDefault() as JsonPropertyNameAttribute;
        Assert.Equal("defaultLogPath", defaultLogPathAttribute?.Name);
    }

    [Fact]
    public void SettingsProperties_CanBeModified()
    {
        var settings = new ApplicationSettings();
        
        // Test modifying various properties
        settings.FcxMode = true;
        Assert.True(settings.FcxMode);
        
        settings.ShowFormIdValues = true;
        Assert.True(settings.ShowFormIdValues);
        
        settings.MaxConcurrentScans = 32;
        Assert.Equal(32, settings.MaxConcurrentScans);
        
        settings.DefaultOutputFormat = "json";
        Assert.Equal("json", settings.DefaultOutputFormat);
        
        settings.WindowWidth = 1920;
        settings.WindowHeight = 1080;
        Assert.Equal(1920, settings.WindowWidth);
        Assert.Equal(1080, settings.WindowHeight);
    }
}