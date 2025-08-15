using System.Text.Json.Serialization;
using FluentAssertions;
using Scanner111.Core.Models;

namespace Scanner111.Tests.Models;

public class ApplicationSettingsTests
{
    [Fact]
    public void Constructor_InitializesDefaultValues()
    {
        var settings = new ApplicationSettings();

        settings.FcxMode.Should().BeFalse("FcxMode should be false by default");
        settings.ShowFormIdValues.Should().BeFalse("ShowFormIdValues should be false by default");
        settings.SimplifyLogs.Should().BeFalse("SimplifyLogs should be false by default");
        settings.MoveUnsolvedLogs.Should().BeFalse("MoveUnsolvedLogs should be false by default");
        settings.VrMode.Should().BeFalse("VrMode should be false by default");
        settings.DefaultLogPath.Should().Be("", "DefaultLogPath should be empty by default");
        settings.DefaultGamePath.Should().Be("", "DefaultGamePath should be empty by default");
        settings.GamePath.Should().Be("", "GamePath should be empty by default");
        settings.DefaultScanDirectory.Should().Be("", "DefaultScanDirectory should be empty by default");
        settings.CrashLogsDirectory.Should().Be("", "CrashLogsDirectory should be empty by default");
        settings.BackupDirectory.Should().Be("", "BackupDirectory should be empty by default");
        settings.ModsFolder.Should().Be("", "ModsFolder should be empty by default");
        settings.IniFolder.Should().Be("", "IniFolder should be empty by default");
        settings.DefaultOutputFormat.Should().Be("text", "DefaultOutputFormat should be 'text' by default");
        settings.AutoSaveResults.Should().BeTrue("AutoSaveResults should be true by default");
        settings.AutoLoadF4SeLogs.Should().BeTrue("AutoLoadF4SeLogs should be true by default");
        settings.SkipXseCopy.Should().BeFalse("SkipXseCopy should be false by default");
        settings.MaxConcurrentScans.Should().Be(16, "MaxConcurrentScans should be 16 by default");
        settings.CacheEnabled.Should().BeTrue("CacheEnabled should be true by default");
        settings.EnableDebugLogging.Should().BeFalse("EnableDebugLogging should be false by default");
        settings.VerboseLogging.Should().BeFalse("VerboseLogging should be false by default");
        settings.AudioNotifications.Should().BeFalse("AudioNotifications should be false by default");
        settings.EnableProgressNotifications.Should().BeTrue("EnableProgressNotifications should be true by default");
        settings.EnableUpdateCheck.Should().BeTrue("EnableUpdateCheck should be true by default");
        settings.UpdateSource.Should().Be("Both", "UpdateSource should be 'Both' by default");
        settings.DisableColors.Should().BeFalse("DisableColors should be false by default");
        settings.DisableProgress.Should().BeFalse("DisableProgress should be false by default");
        settings.RememberWindowSize.Should().BeTrue("RememberWindowSize should be true by default");
        settings.WindowWidth.Should().Be(1200, "WindowWidth should be 1200 by default");
        settings.WindowHeight.Should().Be(800, "WindowHeight should be 800 by default");
        settings.MaxLogMessages.Should().Be(100, "MaxLogMessages should be 100 by default");
        settings.RecentLogFiles.Should().NotBeNull("RecentLogFiles list should be initialized");
        settings.RecentLogFiles.Should().BeEmpty("RecentLogFiles should be empty by default");
        settings.RecentGamePaths.Should().NotBeNull("RecentGamePaths list should be initialized");
        settings.RecentGamePaths.Should().BeEmpty("RecentGamePaths should be empty by default");
        settings.RecentScanDirectories.Should().NotBeNull("RecentScanDirectories list should be initialized");
        settings.RecentScanDirectories.Should().BeEmpty("RecentScanDirectories should be empty by default");
        settings.MaxRecentItems.Should().Be(10, "MaxRecentItems should be 10 by default");
        settings.LastUsedAnalyzers.Should().NotBeNull("LastUsedAnalyzers list should be initialized");
        settings.LastUsedAnalyzers.Should().BeEmpty("LastUsedAnalyzers should be empty by default");
    }

    [Fact]
    public void RecentScanPaths_ReturnsRecentScanDirectories()
    {
        var settings = new ApplicationSettings();
        settings.RecentScanDirectories.Add("path1");
        settings.RecentScanDirectories.Add("path2");

        settings.RecentScanPaths.Should().BeSameAs(settings.RecentScanDirectories,
            "RecentScanPaths should reference the same list as RecentScanDirectories");
        settings.RecentScanPaths.Should().HaveCount(2, "two items should be in the list");
        settings.RecentScanPaths[0].Should().Be("path1", "value should match expected");
        settings.RecentScanPaths[1].Should().Be("path2", "value should match expected");
    }

    [Fact]
    public void AddRecentLogFile_AddsToFrontOfList()
    {
        var settings = new ApplicationSettings();

        settings.AddRecentLogFile("path1");
        settings.AddRecentLogFile("path2");

        settings.RecentLogFiles.Should().HaveCount(2, "two items should be in the list");
        settings.RecentLogFiles[0].Should().Be("path2", "value should match expected");
        settings.RecentLogFiles[1].Should().Be("path1", "value should match expected");
    }

    [Fact]
    public void AddRecentLogFile_RemovesDuplicates()
    {
        var settings = new ApplicationSettings();

        settings.AddRecentLogFile("path1");
        settings.AddRecentLogFile("path2");
        settings.AddRecentLogFile("path1");

        settings.RecentLogFiles.Should().HaveCount(2, "two items should be in the list");
        settings.RecentLogFiles[0].Should().Be("path1", "value should match expected");
        settings.RecentLogFiles[1].Should().Be("path2", "value should match expected");
    }

    [Fact]
    public void AddRecentLogFile_RespectsMaxRecentItems()
    {
        var settings = new ApplicationSettings { MaxRecentItems = 3 };

        settings.AddRecentLogFile("path1");
        settings.AddRecentLogFile("path2");
        settings.AddRecentLogFile("path3");
        settings.AddRecentLogFile("path4");

        settings.RecentLogFiles.Should().HaveCount(3, "count should match expected");
        settings.RecentLogFiles[0].Should().Be("path4", "value should match expected");
        settings.RecentLogFiles[1].Should().Be("path3", "value should match expected");
        settings.RecentLogFiles[2].Should().Be("path2", "value should match expected");
        settings.RecentLogFiles.Should().NotContain("path1", "list should not contain the item");
    }

    [Fact]
    public void AddRecentLogFile_IgnoresEmptyOrNullPaths()
    {
        var settings = new ApplicationSettings();

        settings.AddRecentLogFile(null);
        settings.AddRecentLogFile("");
        settings.AddRecentLogFile("   ");

        settings.RecentLogFiles.Should().BeEmpty("collection should be empty");
    }

    [Fact]
    public void AddRecentGamePath_AddsToFrontOfList()
    {
        var settings = new ApplicationSettings();

        settings.AddRecentGamePath("path1");
        settings.AddRecentGamePath("path2");

        settings.RecentGamePaths.Should().HaveCount(2, "count should match expected");
        settings.RecentGamePaths[0].Should().Be("path2", "value should match expected");
        settings.RecentGamePaths[1].Should().Be("path1", "value should match expected");
    }

    [Fact]
    public void AddRecentGamePath_RemovesDuplicates()
    {
        var settings = new ApplicationSettings();

        settings.AddRecentGamePath("path1");
        settings.AddRecentGamePath("path2");
        settings.AddRecentGamePath("path1");

        settings.RecentGamePaths.Should().HaveCount(2, "count should match expected");
        settings.RecentGamePaths[0].Should().Be("path1", "value should match expected");
        settings.RecentGamePaths[1].Should().Be("path2", "value should match expected");
    }

    [Fact]
    public void AddRecentGamePath_RespectsMaxRecentItems()
    {
        var settings = new ApplicationSettings { MaxRecentItems = 3 };

        settings.AddRecentGamePath("path1");
        settings.AddRecentGamePath("path2");
        settings.AddRecentGamePath("path3");
        settings.AddRecentGamePath("path4");

        settings.RecentGamePaths.Should().HaveCount(3, "count should match expected");
        settings.RecentGamePaths[0].Should().Be("path4", "value should match expected");
        settings.RecentGamePaths[1].Should().Be("path3", "value should match expected");
        settings.RecentGamePaths[2].Should().Be("path2", "value should match expected");
        settings.RecentGamePaths.Should().NotContain("path1", "list should not contain the item");
    }

    [Fact]
    public void AddRecentGamePath_IgnoresEmptyOrNullPaths()
    {
        var settings = new ApplicationSettings();

        settings.AddRecentGamePath(null);
        settings.AddRecentGamePath("");

        settings.RecentGamePaths.Should().BeEmpty("collection should be empty");
    }

    [Fact]
    public void AddRecentScanDirectory_AddsToFrontOfList()
    {
        var settings = new ApplicationSettings();

        settings.AddRecentScanDirectory("path1");
        settings.AddRecentScanDirectory("path2");

        settings.RecentScanDirectories.Should().HaveCount(2, "count should match expected");
        settings.RecentScanDirectories[0].Should().Be("path2", "value should match expected");
        settings.RecentScanDirectories[1].Should().Be("path1", "value should match expected");
    }

    [Fact]
    public void AddRecentScanDirectory_RemovesDuplicates()
    {
        var settings = new ApplicationSettings();

        settings.AddRecentScanDirectory("path1");
        settings.AddRecentScanDirectory("path2");
        settings.AddRecentScanDirectory("path1");

        settings.RecentScanDirectories.Should().HaveCount(2, "count should match expected");
        settings.RecentScanDirectories[0].Should().Be("path1", "value should match expected");
        settings.RecentScanDirectories[1].Should().Be("path2", "value should match expected");
    }

    [Fact]
    public void AddRecentScanDirectory_RespectsMaxRecentItems()
    {
        var settings = new ApplicationSettings { MaxRecentItems = 3 };

        settings.AddRecentScanDirectory("path1");
        settings.AddRecentScanDirectory("path2");
        settings.AddRecentScanDirectory("path3");
        settings.AddRecentScanDirectory("path4");

        settings.RecentScanDirectories.Should().HaveCount(3, "count should match expected");
        settings.RecentScanDirectories[0].Should().Be("path4", "value should match expected");
        settings.RecentScanDirectories[1].Should().Be("path3", "value should match expected");
        settings.RecentScanDirectories[2].Should().Be("path2", "value should match expected");
        settings.RecentScanDirectories.Should().NotContain("path1", "list should not contain the item");
    }

    [Fact]
    public void AddRecentScanDirectory_IgnoresEmptyOrNullPaths()
    {
        var settings = new ApplicationSettings();

        settings.AddRecentScanDirectory(null);
        settings.AddRecentScanDirectory("");

        settings.RecentScanDirectories.Should().BeEmpty("collection should be empty");
    }

    [Fact]
    public void AddRecentPath_DelegatesToAddRecentScanDirectory()
    {
        var settings = new ApplicationSettings();

        settings.AddRecentPath("path1");
        settings.AddRecentPath("path2");

        settings.RecentScanDirectories.Should().HaveCount(2, "count should match expected");
        settings.RecentScanDirectories[0].Should().Be("path2", "value should match expected");
        settings.RecentScanDirectories[1].Should().Be("path1", "value should match expected");
    }

    [Fact]
    public void RecentItemsManagement_IsThreadSafe()
    {
        var settings = new ApplicationSettings { MaxRecentItems = 10 };
        var tasks = new List<Task>();

        // Add items concurrently from multiple threads
        for (var i = 0; i < 100; i++)
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
        settings.RecentLogFiles.Count.Should()
            .BeLessThanOrEqualTo(settings.MaxRecentItems, "value should be within limit");
        settings.RecentGamePaths.Count.Should()
            .BeLessThanOrEqualTo(settings.MaxRecentItems, "value should be within limit");
        settings.RecentScanDirectories.Count.Should()
            .BeLessThanOrEqualTo(settings.MaxRecentItems, "value should be within limit");

        // Verify no null entries were added during concurrent access
        settings.RecentLogFiles.Should().NotContain((string)null!, "list should not contain null");
        settings.RecentGamePaths.Should().NotContain((string)null!, "list should not contain null");
        settings.RecentScanDirectories.Should().NotContain((string)null!, "list should not contain null");
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
        fcxModeAttribute?.Name.Should().Be("fcxMode", "value should match expected");

        var showFormIdProperty = type.GetProperty(nameof(ApplicationSettings.ShowFormIdValues));
        var showFormIdAttribute = showFormIdProperty?.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false)
            .FirstOrDefault() as JsonPropertyNameAttribute;
        showFormIdAttribute?.Name.Should().Be("showFormIdValues", "value should match expected");

        var defaultLogPathProperty = type.GetProperty(nameof(ApplicationSettings.DefaultLogPath));
        var defaultLogPathAttribute = defaultLogPathProperty
            ?.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false)
            .FirstOrDefault() as JsonPropertyNameAttribute;
        defaultLogPathAttribute?.Name.Should().Be("defaultLogPath", "value should match expected");
    }

    [Fact]
    public void SettingsProperties_CanBeModified()
    {
        var settings = new ApplicationSettings();

        // Test modifying various properties
        settings.FcxMode = true;
        settings.FcxMode.Should().BeTrue("condition should be true");

        settings.ShowFormIdValues = true;
        settings.ShowFormIdValues.Should().BeTrue("condition should be true");

        settings.MaxConcurrentScans = 32;
        settings.MaxConcurrentScans.Should().Be(32, "value should match expected");

        settings.DefaultOutputFormat = "json";
        settings.DefaultOutputFormat.Should().Be("json", "value should match expected");

        settings.WindowWidth = 1920;
        settings.WindowHeight = 1080;
        settings.WindowWidth.Should().Be(1920, "value should match expected");
        settings.WindowHeight.Should().Be(1080, "value should match expected");
    }
}