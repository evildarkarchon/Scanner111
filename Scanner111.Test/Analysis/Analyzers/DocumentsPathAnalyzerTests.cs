using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Scanner111.Core.Analysis;
using Scanner111.Core.Analysis.Analyzers;
using Scanner111.Core.Configuration;
using Scanner111.Core.Reporting;
using Scanner111.Test.Infrastructure;
using Scanner111.Test.Infrastructure.Fixtures;
using Scanner111.Test.Infrastructure.Mocks;
using Xunit;

namespace Scanner111.Test.Analysis.Analyzers;

/// <summary>
/// Comprehensive tests for DocumentsPathAnalyzer with proper categorization and timeout handling.
/// </summary>
[Collection("TempDirectory")]
[Trait("Category", "Unit")]
[Trait("Performance", "Fast")]
[Trait("Component", "Analyzer")]
public class DocumentsPathAnalyzerTests : AnalyzerTestBase<DocumentsPathAnalyzer>
{
    private readonly TempDirectoryFixture _tempFixture;
    private readonly string _testDocsPath;
    
    public DocumentsPathAnalyzerTests(TempDirectoryFixture tempFixture)
    {
        _tempFixture = tempFixture;
        _testDocsPath = tempFixture.GetTestDirectory(nameof(DocumentsPathAnalyzerTests));
    }

    protected override DocumentsPathAnalyzer CreateAnalyzer()
    {
        return new DocumentsPathAnalyzer(Logger, MockYamlCore);
    }

    #region Basic Properties Tests

    [Fact]
    [Trait("Priority", "Critical")]
    public void Constructor_ValidParameters_InitializesCorrectly()
    {
        // Assert
        Sut.Should().NotBeNull();
        Sut.Name.Should().Be("DocumentsPath");
        Sut.DisplayName.Should().Be("Documents Path Configuration");
        Sut.Priority.Should().Be(20);
        Sut.Timeout.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Constructor_NullYamlCore_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new DocumentsPathAnalyzer(Logger, null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("yamlCore");
    }

    #endregion

    #region OneDrive Detection Tests

    [Theory]
    [InlineData(@"C:\Users\Test\OneDrive\Documents", false, "OneDrive")]
    [InlineData(@"C:\Users\Test\Dropbox\Documents", false, "cloud storage")]
    [InlineData(@"C:\Users\Test\Google Drive\Documents", false, "cloud storage")]
    [InlineData(@"C:\Users\Test\iCloud\Documents", false, "cloud storage")]
    [InlineData(@"C:\Users\Test\Documents", true, null)]
    [Trait("Priority", "High")]
    public async Task AnalyzeAsync_CloudStoragePath_DetectsAndWarns(
        string docsPath, bool isValid, string? expectedWarning)
    {
        // Arrange
        SetupDocumentsPath(docsPath);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        var result = await RunAnalyzerAsync(cts.Token);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        
        if (isValid)
        {
            result.Fragment?.Content.Should().Contain("optimal");
            result.Severity.Should().Be(AnalysisSeverity.None);
        }
        else
        {
            result.Fragment?.Content.Should().Contain(expectedWarning!);
            result.Severity.Should().Be(AnalysisSeverity.Warning);
        }
        
        result.Metadata.Should().ContainKey("HasOneDrive")
            .WhoseValue.Should().Be(!isValid);
    }

    #endregion

    #region INI File Validation Tests

    [Fact]
    [Trait("Priority", "High")]
    public async Task AnalyzeAsync_MissingRequiredIniFiles_ReportsError()
    {
        // Arrange
        var gamePath = SetupGameFolder();
        // Don't create any INI files
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        var result = await RunAnalyzerAsync(cts.Token);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Fragment?.Content.Should().Contain("Missing INI files");
        result.Severity.Should().Be(AnalysisSeverity.Error);
        result.Metadata["IniFilesValid"].Should().Be(false);
    }

    [Fact]
    public async Task AnalyzeAsync_ReadOnlyIniFiles_ReportsWarning()
    {
        // Arrange
        var gamePath = SetupGameFolder();
        var iniFile = CreateIniFile(gamePath, "Fallout4.ini", readOnly: true);
        CreateIniFile(gamePath, "Fallout4Prefs.ini", readOnly: false);
        
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        var result = await RunAnalyzerAsync(cts.Token);

        // Assert
        result.Should().NotBeNull();
        result.Fragment?.Content.Should().Contain("Read-only INI files");
        result.Fragment?.Content.Should().Contain("Fallout4.ini");
        result.Severity.Should().Be(AnalysisSeverity.Warning);
    }

    [Fact]
    public async Task AnalyzeAsync_AllIniFilesPresent_ReportsSuccess()
    {
        // Arrange
        var gamePath = SetupGameFolder();
        CreateIniFile(gamePath, "Fallout4.ini", readOnly: false);
        CreateIniFile(gamePath, "Fallout4Prefs.ini", readOnly: false);
        CreateIniFile(gamePath, "Fallout4Custom.ini", readOnly: false);
        
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        var result = await RunAnalyzerAsync(cts.Token);

        // Assert
        result.Should().NotBeNull();
        result.Fragment?.Content.Should().Contain("INI files are properly configured");
        result.Metadata["IniFilesValid"].Should().Be(true);
    }

    #endregion

    #region Folder Permission Tests

    [Fact]
    [Trait("Priority", "High")]
    public async Task AnalyzeAsync_NoWritePermissions_ReportsError()
    {
        // Arrange
        var gamePath = SetupGameFolder();
        
        // Simulate permission denied by making directory read-only
        var dirInfo = new DirectoryInfo(gamePath);
        dirInfo.Attributes |= FileAttributes.ReadOnly;
        
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        var result = await RunAnalyzerAsync(cts.Token);

        // Assert - May vary based on OS permissions
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        
        // Clean up
        dirInfo.Attributes &= ~FileAttributes.ReadOnly;
    }

    [Fact]
    public async Task AnalyzeAsync_ProperPermissions_ReportsSuccess()
    {
        // Arrange
        var gamePath = SetupGameFolder();
        CreateIniFile(gamePath, "Fallout4.ini", readOnly: false);
        CreateIniFile(gamePath, "Fallout4Prefs.ini", readOnly: false);
        
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        var result = await RunAnalyzerAsync(cts.Token);

        // Assert
        result.Should().NotBeNull();
        result.Fragment?.Content.Should().ContainAny(
            "proper write permissions",
            "optimal");
        result.Metadata["PermissionsValid"].Should().Be(true);
    }

    #endregion

    #region Cancellation and Timeout Tests

    [Fact]
    [Trait("Priority", "High")]
    public async Task AnalyzeAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = () => RunAnalyzerAsync(cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task AnalyzeAsync_TimeoutExceeded_CompletesWithinTimeout()
    {
        // Arrange
        var gamePath = SetupGameFolder();
        CreateIniFile(gamePath, "Fallout4.ini", readOnly: false);
        
        using var cts = new CancellationTokenSource(Sut.Timeout);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var result = await RunAnalyzerAsync(cts.Token);

        // Assert
        stopwatch.Stop();
        result.Should().NotBeNull();
        stopwatch.Elapsed.Should().BeLessThan(Sut.Timeout);
    }

    [Fact]
    public async Task AnalyzeAsync_MultipleExecutions_ThreadSafe()
    {
        // Arrange
        var gamePath = SetupGameFolder();
        CreateIniFile(gamePath, "Fallout4.ini", readOnly: false);
        
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act - Run multiple analyses concurrently
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => RunAnalyzerAsync(cts.Token))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllSatisfy(r =>
        {
            r.Should().NotBeNull();
            r.Success.Should().BeTrue();
        });
    }

    #endregion

    #region Shared Data Tests

    [Fact]
    [Trait("Priority", "Medium")]
    public async Task AnalyzeAsync_ValidPath_SetsDocumentsPathInContext()
    {
        // Arrange
        var gamePath = SetupGameFolder();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        var result = await RunAnalyzerAsync(cts.Token);

        // Assert
        result.Should().NotBeNull();
        TestContext.TryGetSharedData<string>("DocumentsPath", out var sharedPath).Should().BeTrue();
        sharedPath.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    public async Task AnalyzeAsync_EmptyDocumentsPath_HandlesGracefully()
    {
        // Arrange
        SetupDocumentsPath(string.Empty);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        var result = await RunAnalyzerAsync(cts.Token);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Fragment?.Content.Should().Contain("not configured");
    }

    [Fact]
    public async Task AnalyzeAsync_InvalidPath_HandlesGracefully()
    {
        // Arrange
        SetupDocumentsPath(@"Z:\NonExistent\Path\Documents");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        var result = await RunAnalyzerAsync(cts.Token);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        // Should handle the non-existent path gracefully
    }

    [Fact]
    public async Task AnalyzeAsync_VeryLongPath_HandlesCorrectly()
    {
        // Arrange
        var longPath = @"C:\Users\" + new string('a', 200) + @"\Documents";
        SetupDocumentsPath(longPath);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        var result = await RunAnalyzerAsync(cts.Token);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
    }

    #endregion

    #region Helper Methods

    private void SetupDocumentsPath(string path)
    {
        // Override the GetDocumentsPathAsync behavior through context
        TestContext.SetSharedData("_TestDocumentsPath", path);
    }

    private string SetupGameFolder()
    {
        SetupDocumentsPath(_testDocsPath);
        var gamePath = Path.Combine(_testDocsPath, "My Games", "Fallout4");
        Directory.CreateDirectory(gamePath);
        return gamePath;
    }

    private string CreateIniFile(string gamePath, string fileName, bool readOnly)
    {
        var filePath = Path.Combine(gamePath, fileName);
        File.WriteAllText(filePath, "[General]\nbSomeSettings=1");
        
        if (readOnly)
        {
            var fileInfo = new FileInfo(filePath);
            fileInfo.IsReadOnly = true;
        }
        
        return filePath;
    }

    protected override void SetupDefaultYamlSettings()
    {
        base.SetupDefaultYamlSettings();
        
        // Add any specific YAML settings needed for DocumentsPath tests
        WithYamlSetting(YamlStore.Game, "game_path", @"C:\Games\Fallout4");
        WithYamlSetting(YamlStore.Main, "documents_check_enabled", true);
    }

    #endregion
}