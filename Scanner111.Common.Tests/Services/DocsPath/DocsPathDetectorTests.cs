using FluentAssertions;
using Scanner111.Common.Models.DocsPath;
using Scanner111.Common.Models.GamePath;
using Scanner111.Common.Services.DocsPath;

namespace Scanner111.Common.Tests.Services.DocsPath;

/// <summary>
/// Tests for the DocsPathDetector class.
/// </summary>
public class DocsPathDetectorTests : IDisposable
{
    private readonly DocsPathDetector _detector;
    private readonly string _tempDirectory;

    public DocsPathDetectorTests()
    {
        _detector = new DocsPathDetector();
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"DocsPathDetectorTests_{Guid.NewGuid():N}");
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

    #region ValidateDocsPath Tests

    [Fact]
    public void ValidateDocsPath_WithValidPath_ReturnsTrue()
    {
        // Arrange
        var docsFolder = Path.Combine(_tempDirectory, "My Games", "Fallout4");
        Directory.CreateDirectory(docsFolder);

        // Act
        var result = _detector.ValidateDocsPath(GameType.Fallout4, docsFolder);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ValidateDocsPath_WithNonexistentPath_ReturnsFalse()
    {
        // Arrange
        var docsFolder = Path.Combine(_tempDirectory, "NonExistent");

        // Act
        var result = _detector.ValidateDocsPath(GameType.Fallout4, docsFolder);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateDocsPath_WithNullPath_ReturnsFalse()
    {
        // Act
        var result = _detector.ValidateDocsPath(GameType.Fallout4, null!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateDocsPath_WithEmptyPath_ReturnsFalse()
    {
        // Act
        var result = _detector.ValidateDocsPath(GameType.Fallout4, string.Empty);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateDocsPath_WithWhitespacePath_ReturnsFalse()
    {
        // Act
        var result = _detector.ValidateDocsPath(GameType.Fallout4, "   ");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region GeneratePaths Tests

    [Fact]
    public void GeneratePaths_ForFallout4_ReturnsCorrectPaths()
    {
        // Arrange
        var rootPath = @"C:\Users\Test\Documents\My Games\Fallout4";

        // Act
        var paths = _detector.GeneratePaths(GameType.Fallout4, rootPath);

        // Assert
        paths.RootPath.Should().Be(rootPath);
        paths.XseFolderPath.Should().Be(Path.Combine(rootPath, "F4SE"));
        paths.PapyrusLogPath.Should().Be(Path.Combine(rootPath, "Logs", "Script", "Papyrus.0.log"));
        paths.WryeBashModCheckerPath.Should().Be(Path.Combine(rootPath, "ModChecker.html"));
        paths.XseLogPath.Should().Be(Path.Combine(rootPath, "F4SE", "f4se.log"));
        paths.MainIniPath.Should().Be(Path.Combine(rootPath, "Fallout4.ini"));
        paths.CustomIniPath.Should().Be(Path.Combine(rootPath, "Fallout4Custom.ini"));
        paths.PrefsIniPath.Should().Be(Path.Combine(rootPath, "Fallout4Prefs.ini"));
    }

    [Fact]
    public void GeneratePaths_ForFallout4VR_ReturnsCorrectPaths()
    {
        // Arrange
        var rootPath = @"C:\Users\Test\Documents\My Games\Fallout4VR";

        // Act
        var paths = _detector.GeneratePaths(GameType.Fallout4VR, rootPath);

        // Assert
        paths.XseFolderPath.Should().Be(Path.Combine(rootPath, "F4SE"));
        paths.XseLogPath.Should().Be(Path.Combine(rootPath, "F4SE", "f4sevr.log"));
        // INI files still use Fallout4 base name
        paths.MainIniPath.Should().Be(Path.Combine(rootPath, "Fallout4.ini"));
        paths.CustomIniPath.Should().Be(Path.Combine(rootPath, "Fallout4Custom.ini"));
        paths.PrefsIniPath.Should().Be(Path.Combine(rootPath, "Fallout4Prefs.ini"));
    }

    [Fact]
    public void GeneratePaths_ForSkyrimSE_ReturnsCorrectPaths()
    {
        // Arrange
        var rootPath = @"C:\Users\Test\Documents\My Games\Skyrim Special Edition";

        // Act
        var paths = _detector.GeneratePaths(GameType.SkyrimSE, rootPath);

        // Assert
        paths.RootPath.Should().Be(rootPath);
        paths.XseFolderPath.Should().Be(Path.Combine(rootPath, "SKSE"));
        paths.XseLogPath.Should().Be(Path.Combine(rootPath, "SKSE", "skse64.log"));
        // Skyrim INI files use "Skyrim" base name
        paths.MainIniPath.Should().Be(Path.Combine(rootPath, "Skyrim.ini"));
        paths.CustomIniPath.Should().Be(Path.Combine(rootPath, "SkyrimCustom.ini"));
        paths.PrefsIniPath.Should().Be(Path.Combine(rootPath, "SkyrimPrefs.ini"));
    }

    [Fact]
    public void GeneratePaths_ForSkyrimVR_ReturnsCorrectPaths()
    {
        // Arrange
        var rootPath = @"C:\Users\Test\Documents\My Games\SkyrimVR";

        // Act
        var paths = _detector.GeneratePaths(GameType.SkyrimVR, rootPath);

        // Assert
        paths.XseFolderPath.Should().Be(Path.Combine(rootPath, "SKSE"));
        paths.XseLogPath.Should().Be(Path.Combine(rootPath, "SKSE", "sksevr.log"));
        // INI files use Skyrim base name
        paths.MainIniPath.Should().Be(Path.Combine(rootPath, "Skyrim.ini"));
        paths.CustomIniPath.Should().Be(Path.Combine(rootPath, "SkyrimCustom.ini"));
        paths.PrefsIniPath.Should().Be(Path.Combine(rootPath, "SkyrimPrefs.ini"));
    }

    [Fact]
    public void GeneratePaths_WithNullPath_ThrowsArgumentException()
    {
        // Act
        var act = () => _detector.GeneratePaths(GameType.Fallout4, null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GeneratePaths_WithEmptyPath_ThrowsArgumentException()
    {
        // Act
        var act = () => _detector.GeneratePaths(GameType.Fallout4, string.Empty);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GeneratePaths_WithWhitespacePath_ThrowsArgumentException()
    {
        // Act
        var act = () => _detector.GeneratePaths(GameType.Fallout4, "   ");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region DetectDocsPathAsync Tests

    [Fact]
    public async Task DetectDocsPathAsync_WithUnknownGameType_ReturnsFailure()
    {
        // Act
        var result = await _detector.DetectDocsPathAsync(GameType.Unknown);

        // Assert
        result.Found.Should().BeFalse();
        result.GameType.Should().Be(GameType.Unknown);
        result.ErrorMessage.Should().Contain("unknown game type");
    }

    [Fact]
    public async Task DetectDocsPathAsync_WithValidCachedPath_ReturnsCacheMethod()
    {
        // Arrange
        var cachedPath = Path.Combine(_tempDirectory, "My Games", "Fallout4");
        Directory.CreateDirectory(cachedPath);

        // Act
        var result = await _detector.DetectDocsPathAsync(GameType.Fallout4, cachedPath);

        // Assert
        result.Found.Should().BeTrue();
        result.DocsPath.Should().Be(cachedPath);
        result.DetectionMethod.Should().Be(DocsPathDetectionMethod.Cache);
    }

    [Fact]
    public async Task DetectDocsPathAsync_WithInvalidCachedPath_FallsBackToOtherMethods()
    {
        // Arrange - provide a cached path that doesn't exist
        var cachedPath = Path.Combine(_tempDirectory, "NonExistent", "Fallout4");

        // Act
        var result = await _detector.DetectDocsPathAsync(GameType.Fallout4, cachedPath);

        // Assert
        // Should either find via registry/environment or fail
        if (result.Found)
        {
            result.DetectionMethod.Should().NotBe(DocsPathDetectionMethod.Cache);
        }
        else
        {
            result.DetectionMethod.Should().Be(DocsPathDetectionMethod.NotFound);
        }
    }

    [Fact]
    public async Task DetectDocsPathAsync_WithNullCachedPath_FallsBackToOtherMethods()
    {
        // Act
        var result = await _detector.DetectDocsPathAsync(GameType.Fallout4, null);

        // Assert
        // Should either find via registry/environment or fail
        if (result.Found)
        {
            result.DetectionMethod.Should().NotBe(DocsPathDetectionMethod.Cache);
        }
    }

    [Fact]
    public async Task DetectDocsPathAsync_WhenRealDocsPathExists_ReturnsSuccess()
    {
        // This test may find the real Documents folder if it exists
        // Act
        var result = await _detector.DetectDocsPathAsync(GameType.Fallout4);

        // Assert
        if (result.Found)
        {
            result.DocsPath.Should().NotBeNullOrEmpty();
            result.DetectionMethod.Should().BeOneOf(
                DocsPathDetectionMethod.Registry,
                DocsPathDetectionMethod.EnvironmentFallback);
            // Validate the path exists
            Directory.Exists(result.DocsPath).Should().BeTrue();
        }
    }

    [Theory]
    [InlineData(GameType.Fallout4)]
    [InlineData(GameType.Fallout4VR)]
    [InlineData(GameType.SkyrimSE)]
    [InlineData(GameType.SkyrimVR)]
    public async Task DetectDocsPathAsync_ForAllGameTypes_ReturnsValidResult(GameType gameType)
    {
        // Act
        var result = await _detector.DetectDocsPathAsync(gameType);

        // Assert
        result.GameType.Should().Be(gameType);
        // Either found or not found, but should have valid state
        if (result.Found)
        {
            result.DocsPath.Should().NotBeNullOrEmpty();
            result.DetectionMethod.Should().NotBe(DocsPathDetectionMethod.NotFound);
        }
        else
        {
            result.DetectionMethod.Should().Be(DocsPathDetectionMethod.NotFound);
        }
    }

    #endregion

    #region FindWindowsDocumentsFolderAsync Tests

    [Fact]
    public async Task FindWindowsDocumentsFolderAsync_OnWindows_ReturnsPath()
    {
        // This test will only pass on Windows
        if (!OperatingSystem.IsWindows())
        {
            return; // Skip on non-Windows
        }

        // Act
        var result = await _detector.FindWindowsDocumentsFolderAsync();

        // Assert
        result.Should().NotBeNullOrEmpty();
        Directory.Exists(result).Should().BeTrue();
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task DetectDocsPathAsync_SupportsCancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _detector.DetectDocsPathAsync(GameType.Fallout4, cancellationToken: cts.Token));
    }

    [Fact]
    public async Task DetectDocsPathAsync_WithCachedPath_SupportsCancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _detector.DetectDocsPathAsync(GameType.Fallout4, "C:\\Test", cts.Token));
    }

    [Fact]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public async Task FindWindowsDocumentsFolderAsync_SupportsCancellation()
    {
        // This test will only run on Windows
        if (!OperatingSystem.IsWindows())
        {
            return; // Skip on non-Windows
        }

        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _detector.FindWindowsDocumentsFolderAsync(cts.Token));
    }

    #endregion

    #region Integration Tests (OneDrive Detection)

    [Fact]
    public async Task DetectDocsPathAsync_WhenOneDrivePath_ResultHasIsOneDrivePathTrue()
    {
        // Arrange - Create a fake OneDrive docs path
        var oneDrivePath = Path.Combine(_tempDirectory, "OneDrive", "Documents", "My Games", "Fallout4");
        Directory.CreateDirectory(oneDrivePath);

        // Act
        var result = await _detector.DetectDocsPathAsync(GameType.Fallout4, oneDrivePath);

        // Assert
        result.Found.Should().BeTrue();
        result.IsOneDrivePath.Should().BeTrue();
    }

    [Fact]
    public async Task DetectDocsPathAsync_WhenNormalPath_ResultHasIsOneDrivePathFalse()
    {
        // Arrange - Create a normal docs path
        var normalPath = Path.Combine(_tempDirectory, "Documents", "My Games", "Fallout4");
        Directory.CreateDirectory(normalPath);

        // Act
        var result = await _detector.DetectDocsPathAsync(GameType.Fallout4, normalPath);

        // Assert
        result.Found.Should().BeTrue();
        result.IsOneDrivePath.Should().BeFalse();
    }

    #endregion
}
