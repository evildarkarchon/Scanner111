using Moq;
using Scanner111.Models;
using Scanner111.Services;
using WarningDatabase = Scanner111.Models.WarningDatabase;

namespace Scanner111.Tests.Services;

public class CrashAnalysisServiceTests
{
    private readonly AppSettings _appSettings;
    private readonly CrashStackAnalysis _crashStackAnalysis;
    private readonly Mock<FormIdDatabaseService> _mockFormIdDatabaseService;
    private readonly Mock<IYamlSettingsCacheService> _mockYamlSettingsCache;
    private readonly CrashAnalysisService _service;
    private readonly WarningDatabase _warningDatabase;

    public CrashAnalysisServiceTests()
    {
        _appSettings = new AppSettings
        {
            ShowFormIdValues = true
        };
        _warningDatabase = new WarningDatabase();
        _crashStackAnalysis = new CrashStackAnalysis();
        _mockYamlSettingsCache = new Mock<IYamlSettingsCacheService>();
        _mockFormIdDatabaseService = new Mock<FormIdDatabaseService>();

        _service = new CrashAnalysisService(
            _appSettings,
            _warningDatabase,
            _crashStackAnalysis,
            _mockYamlSettingsCache.Object,
            _mockFormIdDatabaseService.Object
        );
    }

    [Fact]
    public void AnalyzeCrashLog_WithNullOrEmptyCallStack_DoesNotThrowException()
    {
        // Arrange
        var parsedLog = new ParsedCrashLog("test.log", new List<string>());
        var issues = new List<LogIssue>();

        // Act & Assert
        var exception = Record.Exception(() => _service.AnalyzeCrashLog(parsedLog, issues));
        Assert.Null(exception);
    }

    [Fact]
    public void ScanForFormIDMatches_WithFormIdDatabaseDisabled_DoesNotAddIssues()
    {
        // Arrange
        _appSettings.ShowFormIdValues = false;
        var parsedLog = new ParsedCrashLog("test.log", new List<string>());
        parsedLog.CallStack.Add("Some call with FormID 0123ABCD in it");
        var issues = new List<LogIssue>();

        // Act
        _service.ScanForFormIdMatches(parsedLog, issues);

        // Assert
        Assert.Empty(issues);
    }

    [Fact]
    public void ScanForFormIDMatches_WithNoDatabaseAvailable_DoesNotAddIssues()
    {
        // Arrange
        _appSettings.ShowFormIdValues = true;
        _mockFormIdDatabaseService.Setup(x => x.DatabaseExists()).Returns(false);

        var parsedLog = new ParsedCrashLog("test.log", new List<string>());
        parsedLog.CallStack.Add("Some call with FormID 0123ABCD in it");
        var issues = new List<LogIssue>();

        // Act
        _service.ScanForFormIdMatches(parsedLog, issues);

        // Assert
        Assert.Empty(issues);
    }

    [Fact]
    public void ScanForFormIDMatches_WithValidFormIds_AddsIssues()
    {
        // Arrange
        _appSettings.ShowFormIdValues = true;
        _mockFormIdDatabaseService.Setup(x => x.DatabaseExists()).Returns(true);
        _mockFormIdDatabaseService
            .Setup(x => x.GetEntry("23ABCD", "Fallout4.esm"))
            .Returns("WEAP: Combat Rifle");

        var parsedLog = new ParsedCrashLog("test.log", new List<string>());
        parsedLog.CallStack.Add("Some call with FormID 0123ABCD in it");
        parsedLog.LoadedPlugins["Fallout4.esm"] = "01";
        var issues = new List<LogIssue>();

        // Act
        _service.ScanForFormIdMatches(parsedLog, issues);

        // Assert
        Assert.Single(issues);
        Assert.Equal("FormID_0123ABCD_Fallout4.esm", issues[0].IssueId);
        Assert.Equal("FormID Found: 0123ABCD", issues[0].Title);
        Assert.Contains("WEAP: Combat Rifle", issues[0].Details);
    }

    [Fact]
    public void ScanForFormIDMatches_WithUnknownFormIds_AddsInformationalIssues()
    {
        // Arrange
        _appSettings.ShowFormIdValues = true;
        _mockFormIdDatabaseService.Setup(x => x.DatabaseExists()).Returns(true);

        var parsedLog = new ParsedCrashLog("test.log", new List<string>());
        parsedLog.CallStack.Add("Some call with FormID FFFFFFFF in it");
        var issues = new List<LogIssue>();

        // Act
        _service.ScanForFormIdMatches(parsedLog, issues);

        // Assert
        Assert.Single(issues);
        Assert.Equal("FormID_FFFFFFFF_Unknown", issues[0].IssueId);
        Assert.Equal(SeverityLevel.Information, issues[0].Severity);
    }

    [Fact]
    public void ScanForPluginMatches_WithPluginReferences_AddsIssues()
    {
        // Arrange
        var parsedLog = new ParsedCrashLog("test.log", new List<string>());
        parsedLog.CallStack.Add("Some call referencing MyMod.esp+0x1234");
        var issues = new List<LogIssue>();

        // Act
        _service.ScanForPluginMatches(parsedLog, issues);

        // Assert
        Assert.Single(issues);
        Assert.Equal("PluginReference", issues[0].IssueId);
        Assert.Contains("MyMod.esp", issues[0].Message);
    }

    [Fact]
    public void ScanForNamedRecords_WithRecordReferences_AddsIssues()
    {
        // Arrange
        var parsedLog = new ParsedCrashLog("test.log", new List<string>());
        parsedLog.CallStack.Add("Some call referencing NPC_ record");
        var issues = new List<LogIssue>();

        // Act
        _service.ScanForNamedRecords(parsedLog, issues);

        // Assert
        Assert.Single(issues);
        Assert.Equal("RecordType_NPC_", issues[0].IssueId);
        Assert.Contains("NPC_", issues[0].Title);
    }

    [Fact]
    public void ScanForMainErrorSuspects_WithMemoryError_AddsCriticalIssue()
    {
        // Arrange
        var parsedLog = new ParsedCrashLog("test.log", new List<string>());
        parsedLog.MainErrorSegment.Add("Unhandled exception: out of memory");
        var issues = new List<LogIssue>();

        // Act
        _service.ScanForMainErrorSuspects(parsedLog, issues);

        // Assert
        Assert.Single(issues);
        Assert.Equal("MemoryError", issues[0].IssueId);
        Assert.Equal(SeverityLevel.Critical, issues[0].Severity);
    }

    [Fact]
    public void ScanForMainErrorSuspects_WithNullReference_AddsCriticalIssue()
    {
        // Arrange
        var parsedLog = new ParsedCrashLog("test.log", new List<string>());
        parsedLog.MainErrorSegment.Add("Unhandled exception: null pointer");
        var issues = new List<LogIssue>();

        // Act
        _service.ScanForMainErrorSuspects(parsedLog, issues);

        // Assert
        Assert.NotEmpty(issues);
        Assert.Contains(issues, i => i.IssueId == "NullReferenceError");
        Assert.Equal(SeverityLevel.Critical, issues.First(i => i.IssueId == "NullReferenceError").Severity);
    }

    [Fact]
    public void ScanForMainErrorSuspects_WithGraphicsError_AddsCriticalIssue()
    {
        // Arrange
        var parsedLog = new ParsedCrashLog("test.log", new List<string>());
        parsedLog.MainErrorSegment.Add("Unhandled exception: directx error");
        var issues = new List<LogIssue>();

        // Act
        _service.ScanForMainErrorSuspects(parsedLog, issues);

        // Assert
        Assert.NotEmpty(issues);
        Assert.Contains(issues, i => i.IssueId == "GraphicsError");
        Assert.Equal(SeverityLevel.Critical, issues.First(i => i.IssueId == "GraphicsError").Severity);
    }

    [Fact]
    public void AnalyzeCrashLog_WithCompleteParsedLog_RunsAllAnalyses()
    {
        // Arrange
        _mockFormIdDatabaseService.Setup(x => x.DatabaseExists()).Returns(true);

        var parsedLog = new ParsedCrashLog("test.log", new List<string>());
        parsedLog.MainErrorSegment.Add("Unhandled exception: access violation");
        parsedLog.CallStack.Add("Call with FormID 0123ABCD and reference to MyMod.esp");
        parsedLog.CallStack.Add("Another call with ACTI record type");
        parsedLog.LoadedPlugins["Fallout4.esm"] = "00";
        parsedLog.LoadedPlugins["MyMod.esp"] = "01";

        var issues = new List<LogIssue>();

        // Act
        _service.AnalyzeCrashLog(parsedLog, issues);

        // Assert
        Assert.NotEmpty(issues);
        // Should have issues from multiple analysis methods
        Assert.Contains(issues, i => i.Source == "MainErrorAnalysis");
        Assert.Contains(issues, i => i.Source == "PluginAnalysis");
        Assert.Contains(issues, i => i.Source == "RecordTypeAnalysis");
    }
}