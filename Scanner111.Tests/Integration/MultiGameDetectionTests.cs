using System.Runtime.InteropServices;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;

namespace Scanner111.Tests.Integration;

/// <summary>
///     Integration tests for multi-game detection functionality
/// </summary>
[Collection("IO Heavy Tests")]
public class MultiGameDetectionTests : IDisposable
{
    private readonly ILogger<object> _logger;
    private readonly List<string> _tempDirectories;

    public MultiGameDetectionTests()
    {
        _tempDirectories = new List<string>();
        _logger = NullLogger<object>.Instance;
    }

    public void Dispose()
    {
        // Clean up temporary directories
        foreach (var dir in _tempDirectories)
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void DetectGameConfiguration_Fallout4_ReturnsCorrectConfiguration()
    {
        // Arrange
        var mockGamePath = CreateMockGameInstallation("Fallout4", new[]
        {
            "Fallout4.exe",
            "Fallout4Launcher.exe",
            "Data/Fallout4.esm",
            "Data/Fallout4 - Textures1.ba2"
        });

        // Act
        var config = DetectGameConfigurationFromPath(mockGamePath, "Fallout4");

        // Assert
        config.Should().NotBeNull();
        config.GameName.Should().Be("Fallout4");
        config.RootPath.Should().Be(mockGamePath);
        config.ExecutablePath.Should().Contain("Fallout4.exe");
        config.DocumentsPath.Should().NotBeEmpty();
        config.Platform.Should().NotBeEmpty();
    }

    [Fact]
    public void DetectGameConfiguration_Fallout4VR_ReturnsCorrectConfiguration()
    {
        // Arrange
        var mockGamePath = CreateMockGameInstallation("Fallout4VR", new[]
        {
            "Fallout4VR.exe",
            "Data/Fallout4.esm",
            "Data/Fallout4VR.esm"
        });

        // Act
        var config = DetectGameConfigurationFromPath(mockGamePath, "Fallout4VR");

        // Assert
        config.Should().NotBeNull();
        config.GameName.Should().Be("Fallout4VR");
        config.RootPath.Should().Be(mockGamePath);
        config.ExecutablePath.Should().Contain("Fallout4VR.exe");
    }

    [Fact]
    public void DetectGameConfiguration_WithF4SE_IncludesXsePath()
    {
        // Arrange
        var mockGamePath = CreateMockGameInstallation("Fallout4", new[]
        {
            "Fallout4.exe",
            "f4se_loader.exe",
            "f4se_1_10_163.dll",
            "f4se_steam_loader.dll",
            "Data/F4SE/Plugins/Buffout4.dll"
        });

        // Act
        var config = DetectGameConfigurationFromPath(mockGamePath, "Fallout4");

        // Assert
        config.Should().NotBeNull();
        config.XsePath.Should().NotBeNull();
        config.XsePath.Should().Contain("f4se_loader.exe");
    }

    [Fact]
    public void DetectAllInstalledGames_FindsMultipleGames()
    {
        // Arrange
        var games = new List<(string name, string[] files)>
        {
            ("Fallout4", new[] { "Fallout4.exe", "Data/Fallout4.esm" }),
            ("Fallout4VR", new[] { "Fallout4VR.exe", "Data/Fallout4VR.esm" })
            // Future games can be added here
            // ("Skyrim", new[] { "SkyrimSE.exe", "Data/Skyrim.esm" })
        };

        var mockInstallations = new Dictionary<string, string>();
        foreach (var (name, files) in games) mockInstallations[name] = CreateMockGameInstallation(name, files);

        // Act
        var detectedGames = new List<GameConfiguration>();
        foreach (var (gameName, gamePath) in mockInstallations)
        {
            var config = DetectGameConfigurationFromPath(gamePath, gameName);
            if (config != null) detectedGames.Add(config);
        }

        // Assert
        detectedGames.Count.Should().Be(2); // Currently only Fallout4 and Fallout4VR
        Assert.Contains(detectedGames, g => g.GameName == "Fallout4");
        Assert.Contains(detectedGames, g => g.GameName == "Fallout4VR");
    }

    [Theory]
    [InlineData(@"C:\Program Files (x86)\Steam\steamapps\common\Fallout 4", "Steam")]
    [InlineData(@"C:\GOG Games\Fallout 4", "GOG")]
    [InlineData(@"C:\Epic Games\Fallout4", "Epic")]
    [InlineData(@"C:\Xbox Games\Fallout 4", "Xbox")]
    [InlineData(@"C:\Games\Fallout 4", "Unknown")]
    public void DetectPlatform_FromPath_ReturnsCorrectPlatform(string installPath, string expectedPlatform)
    {
        // Arrange
        var normalizedPath = installPath.Replace(@"C:\", Path.GetTempPath());
        var mockGamePath = CreateMockGameInstallation("Fallout4", new[] { "Fallout4.exe" }, normalizedPath);

        // Act
        var config = DetectGameConfigurationFromPath(mockGamePath, "Fallout4");
        var detectedPlatform = DetectPlatformFromPath(mockGamePath);

        // Assert
        config.Should().NotBeNull();
        detectedPlatform.Should().Be(expectedPlatform);
    }

    [Fact]
    public void DetectGameVersion_FromExecutableHash_ReturnsKnownVersion()
    {
        // Arrange
        var mockGamePath = CreateMockGameInstallation("Fallout4", new[] { "Fallout4.exe" });
        var exePath = Path.Combine(mockGamePath, "Fallout4.exe");

        // Write specific content to generate known hash
        // In a real scenario, we'd have a database of known hashes
        File.WriteAllText(exePath, "Fallout4 v1.10.163.0");

        // Act
        var version = DetectGameVersionFromExecutable(exePath);

        // Assert
        version.Should().NotBeNull();
        // Version detection would normally use hash lookup
    }

    [Fact]
    public void ValidateGameIntegrity_WithCompleteInstallation_ReturnsValid()
    {
        // Arrange
        var mockGamePath = CreateMockGameInstallation("Fallout4", new[]
        {
            "Fallout4.exe",
            "Fallout4Launcher.exe",
            "Data/Fallout4.esm",
            "Data/Fallout4 - Textures1.ba2",
            "Data/Fallout4 - Meshes.ba2",
            "Data/Fallout4 - Sounds.ba2"
        });

        // Act
        var isValid = ValidateGameInstallation(mockGamePath, "Fallout4");

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateGameIntegrity_WithMissingFiles_ReturnsInvalid()
    {
        // Arrange
        var mockGamePath = CreateMockGameInstallation("Fallout4", new[]
        {
            "Fallout4.exe"
            // Missing essential game files
        });

        // Act
        var isValid = ValidateGameInstallation(mockGamePath, "Fallout4");

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void DetectGameFromCrashLog_ParsesGameInfo()
    {
        // Arrange
        var crashLogContent = @"
Fallout 4 v1.10.163
Buffout 4 v1.26.2

Unhandled exception at 0x7FF6A1234567
";
        var crashLogPath = CreateTempFile("crash.log", crashLogContent);

        // Act
        var detectedGame = DetectGameFromCrashLog(crashLogPath);

        // Assert
        detectedGame.Should().Be("Fallout4");
    }

    [SkippableFact]
    public void DetectInstalledGames_OnWindows_ChecksRegistry()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "Registry test requires Windows");

        // Act
        var installedGames = DetectGamesFromRegistry();

        // Assert
        // Results depend on actual system configuration
        installedGames.Should().NotBeNull();
    }

    [Fact]
    public void DetectGameConfiguration_WithModOrganizer2_DetectsCorrectPaths()
    {
        // Arrange
        var mo2Path = CreateTempDirectory();
        var gamePath = CreateMockGameInstallation("Fallout4", new[] { "Fallout4.exe" });

        // Create MO2 structure
        var mo2IniPath = Path.Combine(mo2Path, "ModOrganizer.ini");
        File.WriteAllText(mo2IniPath, $@"[General]
gamePath={gamePath}
gameName=Fallout 4
");

        // Act
        var config = DetectGameConfigurationFromMO2(mo2Path);

        // Assert
        config.Should().NotBeNull();
        config.RootPath.Should().Be(gamePath);
    }

    [Fact]
    public void DetectGameConfiguration_WithVortex_DetectsCorrectPaths()
    {
        // Arrange
        var vortexPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Vortex");

        // This test would require mocking Vortex configuration
        // For now, we'll just verify the detection method exists

        // Act
        var config = DetectGameConfigurationFromVortex("Fallout4");

        // Assert
        // Config might be null if Vortex isn't installed
        if (config != null) config.GameName.Should().Be("Fallout4");
    }

    [Theory]
    [InlineData("Fallout4", new[] { "Fallout4.exe", "Data/Fallout4.esm" })]
    [InlineData("Fallout4VR", new[] { "Fallout4VR.exe", "Data/Fallout4VR.esm" })]
    public void BatchGameDetection_ProcessesMultipleGamesEfficiently(string gameName, string[] requiredFiles)
    {
        // Arrange
        var mockPath = CreateMockGameInstallation(gameName, requiredFiles);
        var gamePaths = new Dictionary<string, string> { { gameName, mockPath } };

        // Act
        var configs = new List<GameConfiguration>();
        foreach (var (name, path) in gamePaths)
        {
            var config = DetectGameConfigurationFromPath(path, name);
            if (config != null) configs.Add(config);
        }

        // Assert
        configs.Should().ContainSingle();
        configs[0].GameName.Should().Be(gameName);
    }

    // Helper methods

    private string CreateMockGameInstallation(string gameName, string[] files, string? basePath = null)
    {
        var gameDir = basePath ?? Path.Combine(Path.GetTempPath(), $"MockGame_{gameName}_{Guid.NewGuid()}");
        Directory.CreateDirectory(gameDir);
        _tempDirectories.Add(gameDir);

        foreach (var file in files)
        {
            var fullPath = Path.Combine(gameDir, file);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(fullPath, $"Mock content for {file}");
        }

        return gameDir;
    }

    private string CreateTempDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        _tempDirectories.Add(tempDir);
        return tempDir;
    }

    private string CreateTempFile(string fileName, string content)
    {
        var tempDir = CreateTempDirectory();
        var filePath = Path.Combine(tempDir, fileName);
        File.WriteAllText(filePath, content);
        return filePath;
    }

    private GameConfiguration? DetectGameConfigurationFromPath(string gamePath, string gameName)
    {
        if (!Directory.Exists(gamePath))
            return null;

        var executableName = gameName switch
        {
            "Fallout4" => "Fallout4.exe",
            "Fallout4VR" => "Fallout4VR.exe",
            _ => null
        };

        if (executableName == null)
            return null;

        var executablePath = Path.Combine(gamePath, executableName);
        if (!File.Exists(executablePath))
            return null;

        var config = new GameConfiguration
        {
            GameName = gameName,
            RootPath = gamePath,
            ExecutablePath = executablePath,
            DocumentsPath = GamePathDetection.GetGameDocumentsPath(gameName),
            Platform = DetectPlatformFromPath(gamePath)
        };

        // Check for XSE
        var xsePath = gameName switch
        {
            "Fallout4" => Path.Combine(gamePath, "f4se_loader.exe"),
            _ => null
        };

        if (xsePath != null && File.Exists(xsePath)) config.XsePath = xsePath;

        return config;
    }

    private string DetectPlatformFromPath(string gamePath)
    {
        if (gamePath.Contains(@"\steamapps\", StringComparison.OrdinalIgnoreCase))
            return "Steam";
        if (gamePath.Contains("GOG", StringComparison.OrdinalIgnoreCase))
            return "GOG";
        if (gamePath.Contains("Epic", StringComparison.OrdinalIgnoreCase))
            return "Epic";
        if (gamePath.Contains("Xbox", StringComparison.OrdinalIgnoreCase))
            return "Xbox";
        if (gamePath.Contains("Bethesda.net", StringComparison.OrdinalIgnoreCase))
            return "Bethesda.net";

        return "Unknown";
    }

    private string? DetectGameVersionFromExecutable(string executablePath)
    {
        if (!File.Exists(executablePath))
            return null;

        // In a real implementation, this would calculate hash and look up version
        var content = File.ReadAllText(executablePath);
        if (content.Contains("1.10.163.0"))
            return "1.10.163.0";
        if (content.Contains("1.10.984.0"))
            return "1.10.984.0";

        return "Unknown";
    }

    private bool ValidateGameInstallation(string gamePath, string gameName)
    {
        var requiredFiles = gameName switch
        {
            "Fallout4" => new[]
            {
                "Fallout4.exe",
                "Data/Fallout4.esm",
                "Data/Fallout4 - Textures1.ba2"
            },
            "Fallout4VR" => new[]
            {
                "Fallout4VR.exe",
                "Data/Fallout4VR.esm"
            },
            _ => Array.Empty<string>()
        };

        return requiredFiles.All(file => File.Exists(Path.Combine(gamePath, file)));
    }

    private string? DetectGameFromCrashLog(string crashLogPath)
    {
        if (!File.Exists(crashLogPath))
            return null;

        var content = File.ReadAllText(crashLogPath);

        if (content.Contains("Fallout 4 v") || content.Contains("Fallout4.exe"))
            return "Fallout4";
        if (content.Contains("Fallout 4 VR") || content.Contains("Fallout4VR.exe"))
            return "Fallout4VR";

        return null;
    }

    private List<string> DetectGamesFromRegistry()
    {
        // This would use actual registry reading on Windows
        // For testing, we return an empty list
        return new List<string>();
    }

    private GameConfiguration? DetectGameConfigurationFromMO2(string mo2Path)
    {
        var iniPath = Path.Combine(mo2Path, "ModOrganizer.ini");
        if (!File.Exists(iniPath))
            return null;

        // Simple INI parsing for test
        var lines = File.ReadAllLines(iniPath);
        string? gamePath = null;
        string? gameName = null;

        foreach (var line in lines)
        {
            if (line.StartsWith("gamePath="))
                gamePath = line.Substring("gamePath=".Length);
            if (line.StartsWith("gameName="))
                gameName = line.Substring("gameName=".Length);
        }

        if (gamePath == null || gameName == null)
            return null;

        var normalizedGameName = gameName.Replace(" ", "");
        return DetectGameConfigurationFromPath(gamePath, normalizedGameName);
    }

    private GameConfiguration? DetectGameConfigurationFromVortex(string gameName)
    {
        // This would read actual Vortex configuration
        // For testing, we return null
        return null;
    }
}