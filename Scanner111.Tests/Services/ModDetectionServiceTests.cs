using Moq;
using Scanner111.Models;
using Scanner111.Services;
using WarningDatabase = Scanner111.Models.WarningDatabase;

namespace Scanner111.Tests.Services;

public class ModDetectionServiceTests
{
    private readonly Mock<AppSettings> _mockAppSettings;
    private readonly Mock<WarningDatabase> _mockWarningDatabase;
    private readonly Mock<IYamlSettingsCacheService> _mockYamlSettingsCache;
    private readonly ModDetectionService _service;

    public ModDetectionServiceTests()
    {
        _mockYamlSettingsCache = new Mock<IYamlSettingsCacheService>();
        _mockWarningDatabase = new Mock<WarningDatabase>();
        _mockAppSettings = new Mock<AppSettings>();

        _service = new ModDetectionService(
            _mockYamlSettingsCache.Object,
            _mockWarningDatabase.Object,
            _mockAppSettings.Object
        );
    }

    [Fact]
    public void DetectSingleMods_FindsMatchingPlugins()
    {
        // Arrange
        var parsedLog = new ParsedCrashLog("test.log", new List<string>());
        parsedLog.LoadedPlugins["TestPlugin.esp"] = "01";
        parsedLog.LoadedPlugins["AnotherPlugin.esp"] = "02";

        var issues = new List<LogIssue>();

        var singlePluginWarnings = new Dictionary<string, WarningDetails>
        {
            ["TestPlugin.esp"] = new()
            {
                Message = "This plugin causes issues",
                Title = "Test Plugin Issue",
                Severity = SeverityLevel.Warning
            }
        };

        _mockWarningDatabase
            .Setup(x => x.GetSinglePluginWarnings())
            .Returns(singlePluginWarnings);

        // Act
        _service.DetectSingleMods(parsedLog, issues);

        // Assert
        Assert.Single(issues);
        Assert.Equal("Test Plugin Issue", issues[0].Title);
        Assert.Equal("This plugin causes issues", issues[0].Message);
        Assert.Equal(SeverityLevel.Warning, issues[0].Severity);
    }

    [Fact]
    public void DetectModConflicts_FindsConflictingPlugins()
    {
        // Arrange
        var parsedLog = new ParsedCrashLog("test.log", new List<string>());
        parsedLog.LoadedPlugins["PluginA.esp"] = "01";
        parsedLog.LoadedPlugins["PluginB.esp"] = "02";

        var issues = new List<LogIssue>();

        var conflictRules = new List<ConflictRule>
        {
            new()
            {
                PluginA = "PluginA.esp",
                PluginB = "PluginB.esp",
                Message = "These plugins conflict",
                Title = "Conflict Detected",
                Severity = SeverityLevel.Error
            }
        };

        _mockWarningDatabase
            .Setup(x => x.GetPluginConflictWarnings())
            .Returns(conflictRules);

        // Act
        _service.DetectModConflicts(parsedLog, issues);

        // Assert
        Assert.Single(issues);
        Assert.Equal("Conflict Detected", issues[0].Title);
        Assert.Equal("These plugins conflict", issues[0].Message);
        Assert.Equal(SeverityLevel.Error, issues[0].Severity);
    }

    [Fact]
    public void CheckPluginLimits_WarnsWhenPluginLimitApproached()
    {
        // Arrange
        var parsedLog = new ParsedCrashLog("test.log", new List<string>());

        // Add 240 regular plugins (close to the 254 limit)
        for (var i = 0; i < 240; i++) parsedLog.LoadedPlugins[$"Plugin{i:D3}.esp"] = i.ToString("X2");

        var issues = new List<LogIssue>();

        // Act
        _service.CheckPluginLimits(parsedLog, issues);

        // Assert
        Assert.Single(issues);
        Assert.Contains("Approaching Full Plugin Limit", issues[0].Title);
        Assert.Equal(SeverityLevel.Warning, issues[0].Severity);
    }

    [Fact]
    public void CheckPluginLimits_ErrorWhenPluginLimitExceeded()
    {
        // Arrange
        var parsedLog = new ParsedCrashLog("test.log", new List<string>());

        // Add 255 regular plugins (over the 254 limit)
        for (var i = 0; i < 255; i++) parsedLog.LoadedPlugins[$"Plugin{i:D3}.esp"] = i.ToString("X2");

        var issues = new List<LogIssue>();

        // Act
        _service.CheckPluginLimits(parsedLog, issues);

        // Assert
        Assert.Single(issues);
        Assert.Contains("Full Plugin Limit Exceeded", issues[0].Title);
        Assert.Equal(SeverityLevel.Critical, issues[0].Severity);
    }
}