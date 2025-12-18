using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Scanner111.Common.Models.GamePath;
using Scanner111.Common.Services.GamePath;

namespace Scanner111.Common.Tests.Services.GamePath;

/// <summary>
/// Tests for the GamePathDetector class.
/// </summary>
public class GamePathDetectorTests : IDisposable
{
    private readonly GamePathDetector _detector;
    private readonly string _tempDirectory;

    public GamePathDetectorTests()
    {
        _detector = new GamePathDetector(NullLogger<GamePathDetector>.Instance);
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"GamePathDetectorTests_{Guid.NewGuid():N}");
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

    #region ValidateGamePath Tests

    [Theory]
    [InlineData(GameType.Fallout4, "Fallout4.exe")]
    [InlineData(GameType.Fallout4VR, "Fallout4VR.exe")]
    [InlineData(GameType.SkyrimSE, "SkyrimSE.exe")]
    [InlineData(GameType.SkyrimVR, "SkyrimVR.exe")]
    public void ValidateGamePath_WithValidPath_ReturnsTrue(GameType gameType, string exeName)
    {
        // Arrange
        var gameFolder = Path.Combine(_tempDirectory, gameType.ToString());
        Directory.CreateDirectory(gameFolder);
        File.WriteAllText(Path.Combine(gameFolder, exeName), "dummy");

        // Act
        var result = _detector.ValidateGamePath(gameType, gameFolder);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ValidateGamePath_WithMissingExecutable_ReturnsFalse()
    {
        // Arrange
        var gameFolder = Path.Combine(_tempDirectory, "Fallout4");
        Directory.CreateDirectory(gameFolder);
        // Don't create the executable

        // Act
        var result = _detector.ValidateGamePath(GameType.Fallout4, gameFolder);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateGamePath_WithNonexistentPath_ReturnsFalse()
    {
        // Arrange
        var gameFolder = Path.Combine(_tempDirectory, "NonExistent");

        // Act
        var result = _detector.ValidateGamePath(GameType.Fallout4, gameFolder);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateGamePath_WithNullPath_ReturnsFalse()
    {
        // Act
        var result = _detector.ValidateGamePath(GameType.Fallout4, null!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateGamePath_WithEmptyPath_ReturnsFalse()
    {
        // Act
        var result = _detector.ValidateGamePath(GameType.Fallout4, string.Empty);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateGamePath_WithUnknownGameType_ReturnsFalse()
    {
        // Arrange
        var gameFolder = Path.Combine(_tempDirectory, "Unknown");
        Directory.CreateDirectory(gameFolder);

        // Act
        var result = _detector.ValidateGamePath(GameType.Unknown, gameFolder);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region FindFromXseLogAsync Tests

    [Fact]
    public async Task FindFromXseLogAsync_WithValidF4seLog_ExtractsPath()
    {
        // Arrange
        var gameFolder = CreateValidGameFolder(GameType.Fallout4);
        var xseLogPath = CreateXseLog($@"plugin directory = {gameFolder}\Data\F4SE\Plugins");

        // Act
        var result = await _detector.FindFromXseLogAsync(GameType.Fallout4, xseLogPath);

        // Assert
        result.Should().Be(gameFolder);
    }

    [Fact]
    public async Task FindFromXseLogAsync_WithValidSkseLog_ExtractsPath()
    {
        // Arrange
        var gameFolder = CreateValidGameFolder(GameType.SkyrimSE);
        var xseLogPath = CreateXseLog($@"plugin directory = {gameFolder}\Data\SKSE\Plugins");

        // Act
        var result = await _detector.FindFromXseLogAsync(GameType.SkyrimSE, xseLogPath);

        // Assert
        result.Should().Be(gameFolder);
    }

    [Fact]
    public async Task FindFromXseLogAsync_WithForwardSlashes_ExtractsPath()
    {
        // Arrange
        var gameFolder = CreateValidGameFolder(GameType.Fallout4);
        var forwardSlashPath = gameFolder.Replace('\\', '/');
        var xseLogPath = CreateXseLog($"plugin directory = {forwardSlashPath}/Data/F4SE/Plugins");

        // Act
        var result = await _detector.FindFromXseLogAsync(GameType.Fallout4, xseLogPath);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task FindFromXseLogAsync_WithNoPluginDirectoryLine_ReturnsNull()
    {
        // Arrange
        var xseLogPath = CreateFile("f4se.log", "F4SE runtime: 0.6.23\nSome other content\nNo plugin directory here");

        // Act
        var result = await _detector.FindFromXseLogAsync(GameType.Fallout4, xseLogPath);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task FindFromXseLogAsync_WithInvalidPath_ReturnsNull()
    {
        // Arrange - create log with path that doesn't exist
        var xseLogPath = CreateXseLog(@"plugin directory = C:\NonExistent\Game\Data\F4SE\Plugins");

        // Act
        var result = await _detector.FindFromXseLogAsync(GameType.Fallout4, xseLogPath);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task FindFromXseLogAsync_WithMissingLogFile_ReturnsNull()
    {
        // Arrange
        var xseLogPath = Path.Combine(_tempDirectory, "nonexistent.log");

        // Act
        var result = await _detector.FindFromXseLogAsync(GameType.Fallout4, xseLogPath);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task FindFromXseLogAsync_WithNullPath_ReturnsNull()
    {
        // Act
        var result = await _detector.FindFromXseLogAsync(GameType.Fallout4, null!);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task FindFromXseLogAsync_WithEmptyPath_ReturnsNull()
    {
        // Act
        var result = await _detector.FindFromXseLogAsync(GameType.Fallout4, string.Empty);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task FindFromXseLogAsync_WithUnknownGameType_ReturnsNull()
    {
        // Arrange
        var xseLogPath = CreateFile("unknown.log", "plugin directory = C:\\Game\\Data\\XSE\\Plugins");

        // Act
        var result = await _detector.FindFromXseLogAsync(GameType.Unknown, xseLogPath);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task FindFromXseLogAsync_WithTypicalF4seLogContent_ExtractsPath()
    {
        // Arrange - realistic f4se.log content
        var gameFolder = CreateValidGameFolder(GameType.Fallout4);
        var logContent = $"""
            F4SE runtime: 0.6.23, release 0 64-bit
            config path = {gameFolder}\Data\F4SE\f4se.ini
            plugin directory = {gameFolder}\Data\F4SE\Plugins
            checking plugin C:\...
            """;
        var xseLogPath = CreateFile("f4se.log", logContent);

        // Act
        var result = await _detector.FindFromXseLogAsync(GameType.Fallout4, xseLogPath);

        // Assert
        result.Should().Be(gameFolder);
    }

    #endregion

    #region DetectGamePathAsync Tests

    [Fact]
    public async Task DetectGamePathAsync_WithUnknownGameType_ReturnsFailure()
    {
        // Act
        var result = await _detector.DetectGamePathAsync(GameType.Unknown);

        // Assert
        result.Found.Should().BeFalse();
        result.GameType.Should().Be(GameType.Unknown);
        result.ErrorMessage.Should().Contain("unknown game type");
    }

    [Fact]
    public async Task DetectGamePathAsync_WithValidXseLog_ReturnsSuccess()
    {
        // Arrange - Use a game type unlikely to be installed (SkyrimVR)
        // to ensure we test XSE log detection specifically
        var gameFolder = CreateValidGameFolder(GameType.SkyrimVR);
        var xseLogPath = CreateXseLog(GameType.SkyrimVR, $@"plugin directory = {gameFolder}\Data\SKSE\Plugins");

        // Act
        var result = await _detector.DetectGamePathAsync(GameType.SkyrimVR, xseLogPath);

        // Assert
        // Either found via XSE log (if not in registry) or via registry
        result.Found.Should().BeTrue();
        result.GameType.Should().Be(GameType.SkyrimVR);
        // If found via XSE log, path should match our test folder
        if (result.DetectionMethod == GamePathDetectionMethod.XseLog)
        {
            result.GamePath.Should().Be(gameFolder);
        }
    }

    [Fact]
    public async Task DetectGamePathAsync_WhenGameNotInstalled_AndInvalidXseLog_ReturnsFailure()
    {
        // Arrange - Use a game type unlikely to be installed
        // and provide an invalid XSE log
        var xseLogPath = CreateFile("sksevr.log", "no plugin directory line");

        // Act
        var result = await _detector.DetectGamePathAsync(GameType.SkyrimVR, xseLogPath);

        // Assert - either found via registry (if installed) or not found
        if (!result.Found)
        {
            result.DetectionMethod.Should().Be(GamePathDetectionMethod.NotFound);
            result.ErrorMessage.Should().NotBeNullOrEmpty();
        }
        // If found, it must have been via registry since XSE log is invalid
        else
        {
            result.DetectionMethod.Should().BeOneOf(
                GamePathDetectionMethod.Registry,
                GamePathDetectionMethod.GogRegistry);
        }
    }

    [Fact]
    public async Task DetectGamePathAsync_FindsInstalledGame_ReturnsValidPath()
    {
        // This test verifies that when we detect a game, it's actually valid
        // Note: May find a real installation via registry or fail if not installed

        // Act
        var result = await _detector.DetectGamePathAsync(GameType.Fallout4);

        // Assert
        if (result.Found)
        {
            result.GamePath.Should().NotBeNullOrEmpty();
            _detector.ValidateGamePath(GameType.Fallout4, result.GamePath!).Should().BeTrue();
            result.DetectionMethod.Should().NotBe(GamePathDetectionMethod.NotFound);
        }
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task DetectGamePathAsync_SupportsCancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _detector.DetectGamePathAsync(GameType.Fallout4, cancellationToken: cts.Token));
    }

    [Fact]
    public async Task FindFromXseLogAsync_SupportsCancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var xseLogPath = CreateFile("f4se.log", "dummy content");

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _detector.FindFromXseLogAsync(GameType.Fallout4, xseLogPath, cts.Token));
    }

    [Fact]
    public async Task DetectAllInstalledGamesAsync_SupportsCancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _detector.DetectAllInstalledGamesAsync(cts.Token));
    }

    #endregion

    #region Helper Methods

    private string CreateFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(_tempDirectory, relativePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    private string CreateValidGameFolder(GameType gameType)
    {
        var gameFolder = Path.Combine(_tempDirectory, $"Games/{gameType}");
        Directory.CreateDirectory(gameFolder);
        Directory.CreateDirectory(Path.Combine(gameFolder, "Data"));
        File.WriteAllText(Path.Combine(gameFolder, gameType.GetExecutableName()), "dummy exe");
        return gameFolder;
    }

    private string CreateXseLog(string pluginDirectoryLine)
    {
        return CreateXseLog(GameType.Fallout4, pluginDirectoryLine);
    }

    private string CreateXseLog(GameType gameType, string pluginDirectoryLine)
    {
        var logContent = $"""
            XSE runtime: 1.0.0
            {pluginDirectoryLine}
            Loading plugins...
            """;
        var logFileName = gameType.GetXseLogFileName();
        if (string.IsNullOrEmpty(logFileName))
        {
            logFileName = "xse.log";
        }
        return CreateFile(logFileName, logContent);
    }

    #endregion
}
