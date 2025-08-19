using Scanner111.Core.GameScanning;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Tests.TestHelpers;

namespace Scanner111.Tests.GameScanning;

/// <summary>
///     Comprehensive tests for WryeBashChecker.
/// </summary>
[Collection("Settings Test Collection")]
public class WryeBashCheckerTests : IDisposable
{
    private readonly WryeBashChecker _checker;
    private readonly Mock<ILogger<WryeBashChecker>> _mockLogger;
    private readonly TestApplicationSettingsService _settingsService;
    private readonly TestYamlSettingsProvider _yamlProvider;
    private readonly TestFileSystem _fileSystem;
    private readonly TestEnvironmentPathProvider _environment;
    private readonly TestPathService _pathService;
    private readonly string _testDocumentsPath;

    public WryeBashCheckerTests()
    {
        _settingsService = new TestApplicationSettingsService();
        _yamlProvider = new TestYamlSettingsProvider();
        _mockLogger = new Mock<ILogger<WryeBashChecker>>();
        _fileSystem = new TestFileSystem();
        _environment = new TestEnvironmentPathProvider();
        _pathService = new TestPathService();

        _testDocumentsPath = @"C:\Users\TestUser\Documents";
        _environment.SetSpecialFolder(Environment.SpecialFolder.MyDocuments, _testDocumentsPath);

        _checker = new WryeBashChecker(
            _settingsService,
            _yamlProvider,
            _mockLogger.Object,
            _fileSystem,
            _environment,
            _pathService);

        SetupDefaultMocks();
    }

    public void Dispose()
    {
        // No cleanup needed for test implementations
    }

    private void SetupDefaultMocks()
    {
        // Directly modify the Settings property of TestApplicationSettingsService
        _settingsService.Settings.GameType = GameType.Fallout4;
    }

    #region Helper Methods

    private string CreateWryeBashReport(string htmlContent)
    {
        // Create the report in the test file system
        var gameFolderPath = _pathService.Combine(_testDocumentsPath, "My Games", "Fallout4");
        _fileSystem.CreateDirectory(gameFolderPath);

        var reportPath = _pathService.Combine(gameFolderPath, "ModChecker.html");
        _fileSystem.AddFile(reportPath, htmlContent);

        return reportPath;
    }

    #endregion

    #region Basic Functionality Tests

    [Fact]
    public async Task AnalyzeAsync_NoReportFile_ReturnsHelpMessage()
    {
        // Arrange
        // Don't create any report file in the test file system
        // The test file system starts empty, so no report exists

        // Act
        var result = await _checker.AnalyzeAsync();

        // Assert
        result.Should().Contain("Wrye Bash Plugin Checker Report");
        result.Should().Contain("was not found");
        result.Should().Contain("generate this report");
        result.Should().Contain("Plugin Checker");
    }

    [Fact]
    public async Task AnalyzeAsync_ReportExists_ShowsFoundMessage()
    {
        // Arrange
        var reportPath = CreateWryeBashReport("<html><body>Empty Report</body></html>");

        // Act
        var result = await _checker.AnalyzeAsync();

        // Assert
        result.Should().Contain("WRYE BASH PLUGIN CHECKER REPORT WAS FOUND");
        result.Should().Contain("ANALYZING CONTENTS");
        result.Should().Contain("This report is located in your Documents");
        result.Should().Contain("To hide this report, remove *ModChecker.html*");
    }

    [Fact]
    public async Task AnalyzeAsync_AlwaysIncludesResourceLinks()
    {
        // Arrange
        CreateWryeBashReport("<html><body>Test</body></html>");

        // Act
        var result = await _checker.AnalyzeAsync();

        // Assert
        result.Should().Contain("For more info about the above detected problems");
        result.Should().Contain("Advanced Troubleshooting:");
        result.Should().Contain("nexusmods.com/fallout4/articles/4141");
        result.Should().Contain("Wrye Bash Advanced Readme Documentation:");
        result.Should().Contain("wrye-bash.github.io/docs/");
        result.Should().Contain("After resolving any problems, run Plugin Checker in Wrye Bash again");
    }

    #endregion

    #region Game Type Specific Tests

    [Fact]
    public async Task AnalyzeAsync_Fallout4_UsesCorrectPath()
    {
        // Arrange
        var settings = new ApplicationSettings { GameType = GameType.Fallout4 };
        _settingsService.Settings.GameType = settings.GameType;
        if (!string.IsNullOrEmpty(settings.GamePath))
            _settingsService.Settings.GamePath = settings.GamePath;

        // Act
        var result = await _checker.AnalyzeAsync();

        // Assert
        result.Should().Contain("Fallout4");
    }

    [Fact]
    public async Task AnalyzeAsync_Fallout4VR_UsesCorrectPath()
    {
        // Arrange
        var settings = new ApplicationSettings { GameType = GameType.Fallout4VR };
        _settingsService.Settings.GameType = settings.GameType;
        if (!string.IsNullOrEmpty(settings.GamePath))
            _settingsService.Settings.GamePath = settings.GamePath;

        // Act
        var result = await _checker.AnalyzeAsync();

        // Assert
        result.Should().Contain("Fallout4"); // VR uses same folder as regular
    }

    [Fact]
    public async Task AnalyzeAsync_SkyrimSE_UsesCorrectPath()
    {
        // Arrange
        var settings = new ApplicationSettings { GameType = GameType.SkyrimSE };
        _settingsService.Settings.GameType = settings.GameType;
        if (!string.IsNullOrEmpty(settings.GamePath))
            _settingsService.Settings.GamePath = settings.GamePath;

        // Act
        var result = await _checker.AnalyzeAsync();

        // Assert
        result.Should().Contain("Skyrim Special Edition");
    }

    [Fact]
    public async Task AnalyzeAsync_SkyrimVR_UsesCorrectPath()
    {
        // Arrange
        var settings = new ApplicationSettings { GameType = GameType.SkyrimVR };
        _settingsService.Settings.GameType = settings.GameType;
        if (!string.IsNullOrEmpty(settings.GamePath))
            _settingsService.Settings.GamePath = settings.GamePath;

        // Act
        var result = await _checker.AnalyzeAsync();

        // Assert
        result.Should().Contain("Skyrim VR");
    }

    [Fact]
    public async Task AnalyzeAsync_UnknownGameType_ReturnsHelpMessage()
    {
        // Arrange
        var settings = new ApplicationSettings { GameType = GameType.Unknown };
        _settingsService.Settings.GameType = settings.GameType;
        if (!string.IsNullOrEmpty(settings.GamePath))
            _settingsService.Settings.GamePath = settings.GamePath;

        // Act
        var result = await _checker.AnalyzeAsync();

        // Assert
        result.Should().Contain("was not found");
    }

    #endregion

    #region HTML Report Parsing Tests

    [Fact]
    public async Task AnalyzeAsync_ReportWithMissingMasters_DetectsIssue()
    {
        // Arrange
        var htmlContent = @"
<html>
<body>
    <h2>Missing Masters</h2>
    <ul>
        <li>MyMod.esp requires DLC1.esm</li>
        <li>AnotherMod.esp requires DLC2.esm</li>
    </ul>
</body>
</html>";
        CreateWryeBashReport(htmlContent);

        // Act
        var result = await _checker.AnalyzeAsync();

        // Assert
        result.Should().Contain("❌ CRITICAL: Missing Masters detected");
        result.Should().Contain("MyMod.esp");
        result.Should().Contain("DLC1.esm");
    }

    [Fact]
    public async Task AnalyzeAsync_ReportWithDeactivatedPlugins_ShowsWarning()
    {
        // Arrange
        var htmlContent = @"
<html>
<body>
    <h2>Deactivated Plugins</h2>
    <ul>
        <li>DeactivatedMod.esp</li>
        <li>UnusedPlugin.esm</li>
    </ul>
</body>
</html>";
        CreateWryeBashReport(htmlContent);

        // Act
        var result = await _checker.AnalyzeAsync();

        // Assert
        result.Should().Contain("⚠️ WARNING: Deactivated plugins found");
        result.Should().Contain("DeactivatedMod.esp");
        result.Should().Contain("UnusedPlugin.esm");
    }

    [Fact]
    public async Task AnalyzeAsync_ReportWithLoadOrderIssues_ShowsWarning()
    {
        // Arrange
        var htmlContent = @"
<html>
<body>
    <h2>Load Order Issues</h2>
    <p>Plugin A.esp should load before Plugin B.esp</p>
    <p>Master.esm is out of order</p>
</body>
</html>";
        CreateWryeBashReport(htmlContent);

        // Act
        var result = await _checker.AnalyzeAsync();

        // Assert
        result.Should().Contain("⚠️ WARNING: Load order issues detected");
    }

    [Fact]
    public async Task AnalyzeAsync_CleanReport_ShowsNoIssues()
    {
        // Arrange
        var htmlContent = @"
<html>
<body>
    <h1>Plugin Checker Report</h1>
    <p>No issues found. All plugins are properly configured.</p>
</body>
</html>";
        CreateWryeBashReport(htmlContent);

        // Act
        var result = await _checker.AnalyzeAsync();

        // Assert
        result.Should().Contain("✔️ No issues detected in Wrye Bash report");
    }

    [Fact]
    public async Task AnalyzeAsync_ReportWithDirtyPlugins_ShowsWarning()
    {
        // Arrange
        var htmlContent = @"
<html>
<body>
    <h2>Dirty Plugins</h2>
    <table>
        <tr><td>Update.esm</td><td>123 ITMs, 45 UDRs</td></tr>
        <tr><td>DawnGuard.esm</td><td>67 ITMs, 12 UDRs</td></tr>
    </table>
</body>
</html>";
        CreateWryeBashReport(htmlContent);

        // Act
        var result = await _checker.AnalyzeAsync();

        // Assert
        result.Should().Contain("⚠️ WARNING: Dirty plugins detected");
        result.Should().Contain("Update.esm");
        result.Should().Contain("123 ITMs");
        result.Should().Contain("45 UDRs");
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task AnalyzeAsync_CorruptedHtmlFile_HandlesGracefully()
    {
        // Arrange
        CreateWryeBashReport("This is not valid HTML <<<<>>>>");

        // Act
        var result = await _checker.AnalyzeAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain("WRYE BASH PLUGIN CHECKER REPORT WAS FOUND");
        // Should still include resource links even if parsing fails
        result.Should().Contain("Advanced Troubleshooting:");
    }

    [Fact]
    public async Task AnalyzeAsync_EmptyHtmlFile_HandlesGracefully()
    {
        // Arrange
        CreateWryeBashReport("");

        // Act
        var result = await _checker.AnalyzeAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain("WRYE BASH PLUGIN CHECKER REPORT WAS FOUND");
    }

    [Fact]
    public async Task AnalyzeAsync_VeryLargeReport_HandlesEfficiently()
    {
        // Arrange
        var largeHtml = "<html><body>";
        for (var i = 0; i < 1000; i++) largeHtml += $"<p>Plugin_{i}.esp has some issue {i}</p>\n";
        largeHtml += "</body></html>";
        CreateWryeBashReport(largeHtml);

        // Act
        var startTime = DateTime.UtcNow;
        var result = await _checker.AnalyzeAsync();
        var elapsed = DateTime.UtcNow - startTime;

        // Assert
        result.Should().NotBeNull();
        elapsed.TotalMilliseconds.Should().BeLessThan(2000); // Should complete within 2 seconds
    }

    #endregion

    #region Complex Scenario Tests

    [Fact]
    public async Task AnalyzeAsync_ComplexReport_ParsesAllSections()
    {
        // Arrange
        var htmlContent = @"
<html>
<head><title>Wrye Bash Plugin Checker Report</title></head>
<body>
    <h1>Plugin Checker Results</h1>
    
    <h2>Missing Masters</h2>
    <ul>
        <li>CustomWeapons.esp missing: WeaponBase.esm</li>
    </ul>
    
    <h2>Deactivated Plugins</h2>
    <ul>
        <li>OldMod.esp (Deactivated)</li>
    </ul>
    
    <h2>Load Order Issues</h2>
    <p>Unofficial Patch.esp should load before all other mods</p>
    
    <h2>Dirty Plugins</h2>
    <table>
        <tr><th>Plugin</th><th>Issues</th></tr>
        <tr><td>Fallout4.esm</td><td>234 ITMs</td></tr>
    </table>
    
    <h2>Bash Tag Suggestions</h2>
    <ul>
        <li>MyMod.esp: Add {{C.Water}} tag</li>
        <li>Landscape.esp: Add {{C.Climate}} tag</li>
    </ul>
</body>
</html>";
        CreateWryeBashReport(htmlContent);

        // Act
        var result = await _checker.AnalyzeAsync();

        // Assert
        result.Should().Contain("Missing Masters");
        result.Should().Contain("Deactivated plugins");
        result.Should().Contain("Load order issues");
        result.Should().Contain("Dirty plugins");
        result.Should().Contain("CustomWeapons.esp");
        result.Should().Contain("WeaponBase.esm");
        result.Should().Contain("Fallout4.esm");
        result.Should().Contain("234 ITMs");
    }

    [Fact]
    public async Task AnalyzeAsync_ReportWithEslFlagIssues_ShowsWarning()
    {
        // Arrange
        var htmlContent = @"
<html>
<body>
    <h2>ESL Flag Issues</h2>
    <p>The following plugins could be flagged as ESL:</p>
    <ul>
        <li>SmallMod1.esp (253 records)</li>
        <li>SmallMod2.esp (1024 records)</li>
    </ul>
</body>
</html>";
        CreateWryeBashReport(htmlContent);

        // Act
        var result = await _checker.AnalyzeAsync();

        // Assert
        result.Should().Contain("ESL");
        result.Should().Contain("SmallMod1.esp");
        result.Should().Contain("SmallMod2.esp");
    }

    #endregion
}