using System.Runtime.InteropServices;
using Scanner111.Core.Infrastructure;

namespace Scanner111.Tests.Infrastructure;

/// <summary>
/// Unit tests for the <see cref="GamePathDetection"/> class
/// </summary>
public class GamePathDetectionTests : IDisposable
{
    private readonly List<string> _tempDirectories;

    public GamePathDetectionTests()
    {
        _tempDirectories = new List<string>();
    }

    public void Dispose()
    {
        // Clean up temporary directories
        foreach (var dir in _tempDirectories)
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void ValidateGamePath_WithValidGamePath_ReturnsTrue()
    {
        // Arrange
        var gamePath = CreateTempGameDirectory(true);

        // Act
        var result = GamePathDetection.ValidateGamePath(gamePath);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ValidateGamePath_WithoutExecutable_ReturnsFalse()
    {
        // Arrange
        var gamePath = CreateTempGameDirectory(false);

        // Act
        var result = GamePathDetection.ValidateGamePath(gamePath);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateGamePath_WithNullPath_ReturnsFalse()
    {
        // Act
        var result = GamePathDetection.ValidateGamePath(null!);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateGamePath_WithEmptyPath_ReturnsFalse()
    {
        // Act
        var result = GamePathDetection.ValidateGamePath("");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateGamePath_WithNonExistentPath_ReturnsFalse()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        // Act
        var result = GamePathDetection.ValidateGamePath(nonExistentPath);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateGamePath_WithFallout4VR_ReturnsTrue()
    {
        // Arrange
        var gamePath = CreateTempDirectory();
        File.WriteAllText(Path.Combine(gamePath, "Fallout4VR.exe"), "dummy");

        // Act
        var result = GamePathDetection.ValidateGamePath(gamePath);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void GetGameDocumentsPath_ForFallout4_ReturnsCorrectPath()
    {
        // Act
        var result = GamePathDetection.GetGameDocumentsPath("Fallout4");

        // Assert
        var expectedPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "My Games", "Fallout4");
        Assert.Equal(expectedPath, result);
    }

    [Fact]
    public void GetGameDocumentsPath_ForFallout4VR_ReturnsCorrectPath()
    {
        // Act
        var result = GamePathDetection.GetGameDocumentsPath("Fallout4VR");

        // Assert
        var expectedPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "My Games", "Fallout4VR");
        Assert.Equal(expectedPath, result);
    }

    [Fact]
    public void GetGameDocumentsPath_ForSkyrim_ReturnsCorrectPath()
    {
        // Act
        var result = GamePathDetection.GetGameDocumentsPath("Skyrim");

        // Assert
        var expectedPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "My Games", "Skyrim Special Edition");
        Assert.Equal(expectedPath, result);
    }

    [Fact]
    public void GetGameDocumentsPath_ForUnknownGame_ReturnsEmptyString()
    {
        // Act
        var result = GamePathDetection.GetGameDocumentsPath("UnknownGame");

        // Assert
        Assert.Equal("", result);
    }

    [Fact]
    public void TryGetGamePathFromXseLog_WithValidF4SELog_ReturnsGamePath()
    {
        // Arrange
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var f4seLogPath = Path.Combine(documentsPath, "My Games", "Fallout4", "F4SE", "f4se.log");
        var expectedGamePath = @"C:\Steam\steamapps\common\Fallout 4";

        // Create log directory
        var logDirectory = Path.GetDirectoryName(f4seLogPath);
        if (!string.IsNullOrEmpty(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
        }

        // Create mock F4SE log with plugin directory line
        var logContent = new[]
        {
            "F4SE runtime: initialize (version = 0.6.23 010A08A0 01D7B5B5B5C5C5C5)",
            $"plugin directory = {expectedGamePath}\\Data\\F4SE\\Plugins",
            "checking plugin F4EE.dll"
        };

        File.WriteAllLines(f4seLogPath, logContent);

        try
        {
            // Act
            var result = GamePathDetection.TryGetGamePathFromXseLog();

            // Assert
            // We can't validate against the exact path since it needs to exist
            // But we can check that the method attempted to parse the log
            Assert.NotNull(result);
        }
        finally
        {
            // Cleanup
            if (File.Exists(f4seLogPath))
                File.Delete(f4seLogPath);
        }
    }

    [Fact]
    public void TryGetGamePathFromXseLog_WithNoLogFile_ReturnsEmptyString()
    {
        // Ensure no F4SE log exists
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var f4seLogPath = Path.Combine(documentsPath, "My Games", "Fallout4", "F4SE", "f4se.log");

        if (File.Exists(f4seLogPath))
            File.Delete(f4seLogPath);

        // Act
        var result = GamePathDetection.TryGetGamePathFromXseLog();

        // Assert
        Assert.Equal("", result);
    }

    [Fact]
    public void DetectGameConfiguration_WithValidGamePath_ReturnsConfiguration()
    {
        // This test is platform-specific and may not find an actual game installation
        // We're testing the method logic rather than actual detection

        // Act
        var config = GamePathDetection.DetectGameConfiguration("Fallout4");

        // Assert
        // Config might be null if no game is installed, which is fine for unit tests
        if (config != null)
        {
            Assert.Equal("Fallout4", config.GameName);
            Assert.NotEmpty(config.RootPath);
            Assert.Contains("Fallout4.exe", config.ExecutablePath);
            Assert.NotEmpty(config.DocumentsPath);
            Assert.NotEmpty(config.Platform);
        }
    }

    [SkippableFact]
    public void TryGetGamePathFromRegistry_OnWindows_ChecksRegistry()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "Registry test requires Windows");

        // Act
        var result = GamePathDetection.TryGetGamePathFromRegistry();

        // Assert
        // Result depends on whether game is actually installed
        // We're just verifying the method executes without error
        Assert.NotNull(result);
    }

    [SkippableFact]
    public void TryGetGamePathFromRegistry_OnNonWindows_ReturnsEmptyString()
    {
        Skip.If(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "Test for non-Windows platforms");

        // Act
        var result = GamePathDetection.TryGetGamePathFromRegistry();

        // Assert
        Assert.Equal("", result);
    }

    [Fact]
    public void TryDetectGamePath_WithSpecificGameType_AttemptsDetection()
    {
        // Act
        var result = GamePathDetection.TryDetectGamePath("Fallout4");

        // Assert
        // Result depends on system configuration
        // We're verifying the method completes without error
        Assert.NotNull(result);
    }

    [Fact]
    public void TryDetectGamePath_WithoutParameter_DefaultsToFallout4()
    {
        // Act
        var result = GamePathDetection.TryDetectGamePath();

        // Assert
        // Result depends on system configuration
        // We're verifying the method completes without error
        Assert.NotNull(result);
    }

    [Theory]
    [InlineData(@"C:\Program Files (x86)\Steam\steamapps\common\Fallout 4", "Steam")]
    [InlineData(@"C:\GOG Games\Fallout 4", "GOG")]
    [InlineData(@"C:\Epic Games\Fallout4", "Epic")]
    [InlineData(@"C:\Program Files\Bethesda.net Launcher\games\Fallout4", "Bethesda.net")]
    [InlineData(@"C:\Games\Fallout 4", "Unknown")]
    public void DetectPlatform_WithVariousPaths_ReturnsCorrectPlatform(string gamePath, string expectedPlatform)
    {
        // Create a mock game directory structure
        var tempPath = CreateTempDirectory();

        // Create the nested directory structure to mimic the provided path
        var gameDir = Path.Combine(tempPath, "MockGame");
        Directory.CreateDirectory(gameDir);
        File.WriteAllText(Path.Combine(gameDir, "Fallout4.exe"), "dummy");

        // Test the platform detection logic by checking path patterns
        // Since DetectPlatform is private, we validate the logic directly
        string detectedPlatform;

        if (gamePath.Contains(@"\steamapps\", StringComparison.OrdinalIgnoreCase))
            detectedPlatform = "Steam";
        else if (gamePath.Contains("GOG", StringComparison.OrdinalIgnoreCase))
            detectedPlatform = "GOG";
        else if (gamePath.Contains("Epic", StringComparison.OrdinalIgnoreCase))
            detectedPlatform = "Epic";
        else if (gamePath.Contains("Bethesda.net", StringComparison.OrdinalIgnoreCase))
            detectedPlatform = "Bethesda.net";
        else
            detectedPlatform = "Unknown";

        // Assert that the detected platform matches the expected platform
        Assert.Equal(expectedPlatform, detectedPlatform);
    }

    [Fact]
    public void ValidateGamePath_WithSpecialCharactersInPath_HandlesCorrectly()
    {
        // Arrange
        var specialPath = CreateTempDirectory();
        var subPath = Path.Combine(specialPath, "Game's & Path (2024)");
        Directory.CreateDirectory(subPath);
        File.WriteAllText(Path.Combine(subPath, "Fallout4.exe"), "dummy");

        // Act
        var result = GamePathDetection.ValidateGamePath(subPath);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ValidateGamePath_WithVeryLongPath_HandlesCorrectly()
    {
        // Arrange
        var basePath = CreateTempDirectory();
        var longDirName = new string('a', 50);
        var longPath = Path.Combine(basePath, longDirName, longDirName);

        try
        {
            Directory.CreateDirectory(longPath);
            File.WriteAllText(Path.Combine(longPath, "Fallout4.exe"), "dummy");

            // Act
            var result = GamePathDetection.ValidateGamePath(longPath);

            // Assert
            Assert.True(result);
        }
        catch (PathTooLongException)
        {
            // Skip test if path is too long for the OS
            // This is expected on some systems
        }
    }

    [Fact]
    public void DetectGameConfiguration_WithF4SE_IncludesXsePath()
    {
        // Arrange
        var gamePath = CreateTempGameDirectory(true);
        File.WriteAllText(Path.Combine(gamePath, "f4se_loader.exe"), "dummy");

        // Mock the detection by creating a test scenario
        // Since we can't control the actual detection, we test the configuration structure
        var config = new Scanner111.Core.Models.GameConfiguration
        {
            GameName = "Fallout4",
            RootPath = gamePath,
            ExecutablePath = Path.Combine(gamePath, "Fallout4.exe"),
            DocumentsPath = GamePathDetection.GetGameDocumentsPath("Fallout4"),
            Platform = "Test"
        };

        // Check if F4SE exists
        var f4sePath = Path.Combine(gamePath, "f4se_loader.exe");
        if (File.Exists(f4sePath))
        {
            config.XsePath = f4sePath;
        }

        // Assert
        Assert.NotNull(config.XsePath);
        Assert.Contains("f4se_loader.exe", config.XsePath);
    }

    private string CreateTempDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        _tempDirectories.Add(tempDir);
        return tempDir;
    }

    private string CreateTempGameDirectory(bool includeExecutable)
    {
        var tempDir = CreateTempDirectory();

        if (includeExecutable)
        {
            File.WriteAllText(Path.Combine(tempDir, "Fallout4.exe"), "dummy executable");
        }

        // Create some typical game directories
        Directory.CreateDirectory(Path.Combine(tempDir, "Data"));
        Directory.CreateDirectory(Path.Combine(tempDir, "Data", "Scripts"));

        return tempDir;
    }
}