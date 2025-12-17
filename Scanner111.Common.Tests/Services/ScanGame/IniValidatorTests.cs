using FluentAssertions;
using Scanner111.Common.Models.ScanGame;
using Scanner111.Common.Services.ScanGame;

namespace Scanner111.Common.Tests.Services.ScanGame;

/// <summary>
/// Tests for the IniValidator class.
/// </summary>
public class IniValidatorTests : IDisposable
{
    private readonly IniValidator _validator;
    private readonly string _tempDirectory;

    public IniValidatorTests()
    {
        _validator = new IniValidator();
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"IniValidatorTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors in tests
        }
        GC.SuppressFinalize(this);
    }

    #region Basic Scanning Tests

    [Fact]
    public async Task ScanAsync_WithEmptyDirectory_ReturnsEmptyResult()
    {
        // Act
        var result = await _validator.ScanAsync(_tempDirectory, "Fallout4");

        // Assert
        result.TotalFilesScanned.Should().Be(0);
        result.HasIssues.Should().BeFalse();
    }

    [Fact]
    public async Task ScanAsync_WithNonExistentDirectory_ReturnsEmptyResult()
    {
        // Arrange
        var nonExistent = Path.Combine(_tempDirectory, "nonexistent");

        // Act
        var result = await _validator.ScanAsync(nonExistent, "Fallout4");

        // Assert
        result.TotalFilesScanned.Should().Be(0);
    }

    [Fact]
    public async Task ScanAsync_WithSingleIniFile_ScansFile()
    {
        // Arrange
        CreateIniFile("test.ini", """
            [Section]
            Key=Value
            """);

        // Act
        var result = await _validator.ScanAsync(_tempDirectory, "Fallout4");

        // Assert
        result.TotalFilesScanned.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ScanAsync_SupportsProgressReporting()
    {
        // Arrange
        CreateIniFile("test1.ini", "[Section]\nKey=Value");
        CreateIniFile("test2.ini", "[Section]\nKey=Value");

        var progressReports = new List<IniScanProgress>();
        var progress = new Progress<IniScanProgress>(p => progressReports.Add(p));

        // Act
        await _validator.ScanWithProgressAsync(_tempDirectory, "Fallout4", progress);

        // Assert - Progress should have been reported
        // Note: May not capture all reports due to async timing
        progressReports.Should().NotBeEmpty();
    }

    #endregion

    #region Console Command Detection Tests

    [Fact]
    public async Task ScanAsync_DetectsConsoleCommandSetting()
    {
        // Arrange
        CreateIniFile("fallout4.ini", """
            [General]
            sStartingConsoleCommand=bat autoexec
            """);

        // Act
        var result = await _validator.ScanAsync(_tempDirectory, "Fallout4");

        // Assert
        result.ConsoleCommandIssues.Should().HaveCount(1);
        result.ConsoleCommandIssues[0].CommandValue.Should().Be("bat autoexec");
    }

    [Fact]
    public async Task ScanAsync_DoesNotDetectConsoleCommand_InNonGameIni()
    {
        // Arrange
        CreateIniFile("other.ini", """
            [General]
            sStartingConsoleCommand=bat autoexec
            """);

        // Act
        var result = await _validator.ScanAsync(_tempDirectory, "Fallout4");

        // Assert
        result.ConsoleCommandIssues.Should().BeEmpty();
    }

    [Fact]
    public async Task ScanAsync_DetectsConsoleCommand_InCustomIni()
    {
        // Arrange
        CreateIniFile("fallout4custom.ini", """
            [General]
            sStartingConsoleCommand=help
            """);

        // Act
        var result = await _validator.ScanAsync(_tempDirectory, "Fallout4");

        // Assert
        result.ConsoleCommandIssues.Should().HaveCount(1);
    }

    #endregion

    #region VSync Detection Tests

    [Fact]
    public async Task ScanAsync_DetectsVSyncInEnblocal()
    {
        // Arrange
        CreateIniFile("enblocal.ini", """
            [ENGINE]
            ForceVSync=true
            """);

        // Act
        var result = await _validator.ScanAsync(_tempDirectory, "Fallout4");

        // Assert
        result.VSyncIssues.Should().HaveCount(1);
        result.VSyncIssues[0].FileName.Should().Be("enblocal.ini");
        result.VSyncIssues[0].Setting.Should().Be("ForceVSync");
        result.VSyncIssues[0].IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task ScanAsync_DetectsVSyncInHighFpsPhysicsFix()
    {
        // Arrange
        CreateIniFile("highfpsphysicsfix.ini", """
            [Main]
            EnableVSync=1
            """);

        // Act
        var result = await _validator.ScanAsync(_tempDirectory, "Fallout4");

        // Assert
        result.VSyncIssues.Should().HaveCount(1);
        result.VSyncIssues[0].Setting.Should().Be("EnableVSync");
    }

    [Fact]
    public async Task ScanAsync_DoesNotDetectDisabledVSync()
    {
        // Arrange
        CreateIniFile("enblocal.ini", """
            [ENGINE]
            ForceVSync=false
            """);

        // Act
        var result = await _validator.ScanAsync(_tempDirectory, "Fallout4");

        // Assert
        result.VSyncIssues.Should().BeEmpty();
    }

    #endregion

    #region Known Issue Detection Tests

    [Fact]
    public async Task ScanAsync_DetectsCommentedHotkey_InEspExplorer()
    {
        // Arrange
        CreateIniFile("espexplorer.ini", """
            [General]
            HotKey=; F10
            """);

        // Act
        var result = await _validator.ScanAsync(_tempDirectory, "Fallout4");

        // Assert
        result.ConfigIssues.Should().HaveCount(1);
        result.ConfigIssues[0].FileName.Should().Be("espexplorer.ini");
        result.ConfigIssues[0].RecommendedValue.Should().Be("0x79");
    }

    [Fact]
    public async Task ScanAsync_DetectsHighParticleCount_InEpo()
    {
        // Arrange
        CreateIniFile("epo.ini", """
            [Particles]
            iMaxDesired=10000
            """);

        // Act
        var result = await _validator.ScanAsync(_tempDirectory, "Fallout4");

        // Assert
        result.ConfigIssues.Should().HaveCount(1);
        result.ConfigIssues[0].Setting.Should().Be("iMaxDesired");
        result.ConfigIssues[0].CurrentValue.Should().Be("10000");
        result.ConfigIssues[0].RecommendedValue.Should().Be("5000");
    }

    [Fact]
    public async Task ScanAsync_DoesNotDetect_AcceptableParticleCount()
    {
        // Arrange
        CreateIniFile("epo.ini", """
            [Particles]
            iMaxDesired=3000
            """);

        // Act
        var result = await _validator.ScanAsync(_tempDirectory, "Fallout4");

        // Assert
        result.ConfigIssues.Should().BeEmpty();
    }

    [Fact]
    public async Task ScanAsync_DetectsLockedHeadParts_InF4ee()
    {
        // Arrange
        CreateIniFile("f4ee.ini", """
            [CharGen]
            bUnlockHeadParts=0
            """);

        // Act
        var result = await _validator.ScanAsync(_tempDirectory, "Fallout4");

        // Assert
        result.ConfigIssues.Should().Contain(i => i.Setting == "bUnlockHeadParts");
    }

    [Fact]
    public async Task ScanAsync_DetectsLockedTints_InF4ee()
    {
        // Arrange
        CreateIniFile("f4ee.ini", """
            [CharGen]
            bUnlockTints=0
            """);

        // Act
        var result = await _validator.ScanAsync(_tempDirectory, "Fallout4");

        // Assert
        result.ConfigIssues.Should().Contain(i => i.Setting == "bUnlockTints");
    }

    [Fact]
    public async Task ScanAsync_DetectsLowLoadingScreenFps()
    {
        // Arrange
        CreateIniFile("highfpsphysicsfix.ini", """
            [Limiter]
            LoadingScreenFPS=60.0
            """);

        // Act
        var result = await _validator.ScanAsync(_tempDirectory, "Fallout4");

        // Assert
        result.ConfigIssues.Should().Contain(i => i.Setting == "LoadingScreenFPS");
    }

    [Fact]
    public async Task ScanAsync_DoesNotDetect_AcceptableLoadingScreenFps()
    {
        // Arrange
        CreateIniFile("highfpsphysicsfix.ini", """
            [Limiter]
            LoadingScreenFPS=800.0
            """);

        // Act
        var result = await _validator.ScanAsync(_tempDirectory, "Fallout4");

        // Assert
        result.ConfigIssues.Where(i => i.Setting == "LoadingScreenFPS").Should().BeEmpty();
    }

    #endregion

    #region Cache Tests

    [Fact]
    public async Task GetValue_ReturnsValue_AfterScan()
    {
        // Arrange
        CreateIniFile("test.ini", """
            [Section]
            IntValue=42
            BoolValue=true
            FloatValue=3.14
            """);

        await _validator.ScanAsync(_tempDirectory, "Fallout4");

        // Act & Assert
        _validator.GetValue<int>("test.ini", "Section", "IntValue").Should().Be(42);
        _validator.GetValue<bool>("test.ini", "Section", "BoolValue").Should().BeTrue();
        _validator.GetValue<float>("test.ini", "Section", "FloatValue").Should().BeApproximately(3.14f, 0.01f);
    }

    [Fact]
    public async Task GetStringValue_ReturnsValue_AfterScan()
    {
        // Arrange
        CreateIniFile("test.ini", """
            [Section]
            Key=SomeValue
            """);

        await _validator.ScanAsync(_tempDirectory, "Fallout4");

        // Act
        var value = _validator.GetStringValue("test.ini", "Section", "Key");

        // Assert
        value.Should().Be("SomeValue");
    }

    [Fact]
    public async Task HasSetting_ReturnsTrue_AfterScan()
    {
        // Arrange
        CreateIniFile("test.ini", """
            [Section]
            Key=Value
            """);

        await _validator.ScanAsync(_tempDirectory, "Fallout4");

        // Act & Assert
        _validator.HasSetting("test.ini", "Section", "Key").Should().BeTrue();
        _validator.HasSetting("test.ini", "Section", "Missing").Should().BeFalse();
    }

    [Fact]
    public async Task ClearCache_RemovesCachedData()
    {
        // Arrange
        CreateIniFile("test.ini", """
            [Section]
            Key=Value
            """);

        await _validator.ScanAsync(_tempDirectory, "Fallout4");
        _validator.HasSetting("test.ini", "Section", "Key").Should().BeTrue();

        // Act
        _validator.ClearCache();

        // Assert
        _validator.HasSetting("test.ini", "Section", "Key").Should().BeFalse();
    }

    #endregion

    #region HasIssues Tests

    [Fact]
    public async Task HasIssues_ReturnsFalse_WhenNoIssues()
    {
        // Arrange
        CreateIniFile("clean.ini", """
            [Section]
            Key=Value
            """);

        // Act
        var result = await _validator.ScanAsync(_tempDirectory, "Fallout4");

        // Assert
        result.HasIssues.Should().BeFalse();
    }

    [Fact]
    public async Task HasIssues_ReturnsTrue_WhenConfigIssuesExist()
    {
        // Arrange
        CreateIniFile("epo.ini", """
            [Particles]
            iMaxDesired=10000
            """);

        // Act
        var result = await _validator.ScanAsync(_tempDirectory, "Fallout4");

        // Assert
        result.HasIssues.Should().BeTrue();
    }

    [Fact]
    public async Task HasIssues_ReturnsTrue_WhenConsoleCommandIssuesExist()
    {
        // Arrange
        CreateIniFile("fallout4.ini", """
            [General]
            sStartingConsoleCommand=help
            """);

        // Act
        var result = await _validator.ScanAsync(_tempDirectory, "Fallout4");

        // Assert
        result.HasIssues.Should().BeTrue();
    }

    [Fact]
    public async Task HasIssues_ReturnsTrue_WhenVSyncIssuesExist()
    {
        // Arrange
        CreateIniFile("enblocal.ini", """
            [ENGINE]
            ForceVSync=true
            """);

        // Act
        var result = await _validator.ScanAsync(_tempDirectory, "Fallout4");

        // Assert
        result.HasIssues.Should().BeTrue();
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task ScanAsync_SupportsCancellation()
    {
        // Arrange
        for (var i = 0; i < 10; i++)
        {
            CreateIniFile($"test{i}.ini", "[Section]\nKey=Value");
        }

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _validator.ScanAsync(_tempDirectory, "Fallout4", cts.Token));
    }

    #endregion

    #region Helper Methods

    private void CreateIniFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(_tempDirectory, relativePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(fullPath, content);
    }

    #endregion
}
