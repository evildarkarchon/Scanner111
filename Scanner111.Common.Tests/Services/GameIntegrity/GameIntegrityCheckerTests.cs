using System.Security.Cryptography;
using FluentAssertions;
using Scanner111.Common.Models.GameIntegrity;
using Scanner111.Common.Models.GamePath;
using Scanner111.Common.Services.GameIntegrity;

namespace Scanner111.Common.Tests.Services.GameIntegrity;

/// <summary>
/// Tests for the GameIntegrityChecker class.
/// </summary>
public class GameIntegrityCheckerTests : IDisposable
{
    private readonly GameIntegrityChecker _checker;
    private readonly string _tempDirectory;

    public GameIntegrityCheckerTests()
    {
        _checker = new GameIntegrityChecker();
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"GameIntegrityCheckerTests_{Guid.NewGuid():N}");
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

    #region Executable Version Tests

    [Fact]
    public async Task CheckExecutableVersionAsync_WithLatestHash_ReturnsLatestVersion()
    {
        // Arrange
        var content = "latest executable content";
        var exePath = CreateFile("Fallout4.exe", content);
        var expectedHash = ComputeHash(content);

        // Act
        var (status, hash, matched) = await _checker.CheckExecutableVersionAsync(
            exePath, "oldhash", expectedHash);

        // Assert
        status.Should().Be(ExecutableVersionStatus.LatestVersion);
        hash.Should().Be(expectedHash);
        matched.Should().Be("new");
    }

    [Fact]
    public async Task CheckExecutableVersionAsync_WithOldHash_ReturnsOutdated()
    {
        // Arrange
        var content = "old executable content";
        var exePath = CreateFile("Fallout4.exe", content);
        var expectedHash = ComputeHash(content);

        // Act
        var (status, hash, matched) = await _checker.CheckExecutableVersionAsync(
            exePath, expectedHash, "newhash");

        // Assert
        status.Should().Be(ExecutableVersionStatus.Outdated);
        hash.Should().Be(expectedHash);
        matched.Should().Be("old");
    }

    [Fact]
    public async Task CheckExecutableVersionAsync_WithUnknownHash_ReturnsUnknown()
    {
        // Arrange
        var exePath = CreateFile("Fallout4.exe", "unknown content");

        // Act
        var (status, hash, matched) = await _checker.CheckExecutableVersionAsync(
            exePath, "oldhash", "newhash");

        // Assert
        status.Should().Be(ExecutableVersionStatus.Unknown);
        hash.Should().NotBeNull();
        matched.Should().BeNull();
    }

    [Fact]
    public async Task CheckExecutableVersionAsync_WithMissingFile_ReturnsNotFound()
    {
        // Arrange
        var exePath = Path.Combine(_tempDirectory, "missing.exe");

        // Act
        var (status, hash, matched) = await _checker.CheckExecutableVersionAsync(
            exePath, "oldhash", "newhash");

        // Assert
        status.Should().Be(ExecutableVersionStatus.NotFound);
        hash.Should().BeNull();
        matched.Should().BeNull();
    }

    [Fact]
    public async Task CheckExecutableVersionAsync_WithNullPath_ReturnsNotChecked()
    {
        // Act
        var (status, hash, matched) = await _checker.CheckExecutableVersionAsync(
            null, "oldhash", "newhash");

        // Assert
        status.Should().Be(ExecutableVersionStatus.NotChecked);
        hash.Should().BeNull();
        matched.Should().BeNull();
    }

    [Fact]
    public async Task CheckExecutableVersionAsync_WithEmptyPath_ReturnsNotChecked()
    {
        // Act
        var (status, hash, matched) = await _checker.CheckExecutableVersionAsync(
            string.Empty, "oldhash", "newhash");

        // Assert
        status.Should().Be(ExecutableVersionStatus.NotChecked);
    }

    [Fact]
    public async Task CheckExecutableVersionAsync_WithBothHashesNull_ReturnsUnknown()
    {
        // Arrange
        var exePath = CreateFile("Fallout4.exe", "some content");

        // Act
        var (status, hash, matched) = await _checker.CheckExecutableVersionAsync(
            exePath, null, null);

        // Assert
        status.Should().Be(ExecutableVersionStatus.Unknown);
        hash.Should().NotBeNull();
        matched.Should().BeNull();
    }

    #endregion

    #region Installation Location Tests

    [Theory]
    [InlineData(@"C:\Program Files\Steam\steamapps\common\Fallout 4", InstallationLocationStatus.RestrictedLocation)]
    [InlineData(@"C:\Program Files (x86)\Steam\steamapps\common\Fallout 4", InstallationLocationStatus.RestrictedLocation)]
    [InlineData(@"D:\Games\Fallout 4", InstallationLocationStatus.RecommendedLocation)]
    [InlineData(@"C:\Steam\steamapps\common\Fallout 4", InstallationLocationStatus.RecommendedLocation)]
    [InlineData(@"E:\SteamLibrary\steamapps\common\Fallout 4", InstallationLocationStatus.RecommendedLocation)]
    public void CheckInstallationLocation_ReturnsCorrectStatus(string path, InstallationLocationStatus expected)
    {
        // Act
        var status = _checker.CheckInstallationLocation(path);

        // Assert
        status.Should().Be(expected);
    }

    [Fact]
    public void CheckInstallationLocation_WithNullPath_ReturnsPathNotProvided()
    {
        // Act
        var status = _checker.CheckInstallationLocation(null);

        // Assert
        status.Should().Be(InstallationLocationStatus.PathNotProvided);
    }

    [Fact]
    public void CheckInstallationLocation_WithEmptyPath_ReturnsPathNotProvided()
    {
        // Act
        var status = _checker.CheckInstallationLocation(string.Empty);

        // Assert
        status.Should().Be(InstallationLocationStatus.PathNotProvided);
    }

    [Fact]
    public void CheckInstallationLocation_IsCaseInsensitive()
    {
        // Act
        var status = _checker.CheckInstallationLocation(@"C:\PROGRAM FILES\Game");

        // Assert
        status.Should().Be(InstallationLocationStatus.RestrictedLocation);
    }

    #endregion

    #region Steam INI Tests

    [Fact]
    public void CheckSteamIniExists_WithExistingFile_ReturnsTrue()
    {
        // Arrange
        var iniPath = CreateFile("steam_api64.ini", "[Steam]\nAppId=377160");

        // Act
        var result = _checker.CheckSteamIniExists(iniPath);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CheckSteamIniExists_WithMissingFile_ReturnsFalse()
    {
        // Arrange
        var iniPath = Path.Combine(_tempDirectory, "nonexistent.ini");

        // Act
        var result = _checker.CheckSteamIniExists(iniPath);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CheckSteamIniExists_WithNullPath_ReturnsFalse()
    {
        // Act
        var result = _checker.CheckSteamIniExists(null);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CheckSteamIniExists_WithEmptyPath_ReturnsFalse()
    {
        // Act
        var result = _checker.CheckSteamIniExists(string.Empty);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Full Integrity Check Tests

    [Fact]
    public async Task CheckIntegrityAsync_WithHealthyInstallation_ReturnsNoIssues()
    {
        // Arrange
        var gameFolder = CreateGameFolder("Fallout 4");
        var exeContent = "healthy exe content";
        CreateFile("Fallout 4/Fallout4.exe", exeContent);
        var newHash = ComputeHash(exeContent);

        var config = new GameIntegrityConfiguration
        {
            GameType = GameType.Fallout4,
            GameDisplayName = "Fallout 4",
            GameRootPath = gameFolder,
            ExecutableHashNew = newHash,
            ExecutableHashOld = "oldhash"
        };

        // Act
        var result = await _checker.CheckIntegrityAsync(config);

        // Assert
        result.HasIssues.Should().BeFalse();
        result.VersionStatus.Should().Be(ExecutableVersionStatus.LatestVersion);
        result.LocationStatus.Should().Be(InstallationLocationStatus.RecommendedLocation);
        result.SteamIniDetected.Should().BeFalse();
        result.ExecutableFound.Should().BeTrue();
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckIntegrityAsync_WithOutdatedExe_ReportsIssue()
    {
        // Arrange
        var gameFolder = CreateGameFolder("Fallout 4");
        var exeContent = "old exe content";
        CreateFile("Fallout 4/Fallout4.exe", exeContent);
        var oldHash = ComputeHash(exeContent);

        var config = new GameIntegrityConfiguration
        {
            GameType = GameType.Fallout4,
            GameDisplayName = "Fallout 4",
            GameRootPath = gameFolder,
            ExecutableHashNew = "newhash",
            ExecutableHashOld = oldHash,
            GameVersionOld = "1.10.163",
            GameVersionNew = "1.10.984"
        };

        // Act
        var result = await _checker.CheckIntegrityAsync(config);

        // Assert
        result.HasIssues.Should().BeTrue();
        result.VersionStatus.Should().Be(ExecutableVersionStatus.Outdated);
        result.MatchedVersion.Should().Be("old");
        result.Issues.Should().Contain(i => i.Type == GameIntegrityIssueType.OutdatedVersion);
        result.Issues.First(i => i.Type == GameIntegrityIssueType.OutdatedVersion)
            .Message.Should().Contain("1.10.163");
    }

    [Fact]
    public async Task CheckIntegrityAsync_WithUnknownExeVersion_ReportsIssue()
    {
        // Arrange
        var gameFolder = CreateGameFolder("Fallout 4");
        CreateFile("Fallout 4/Fallout4.exe", "completely unknown content");

        var config = new GameIntegrityConfiguration
        {
            GameType = GameType.Fallout4,
            GameDisplayName = "Fallout 4",
            GameRootPath = gameFolder,
            ExecutableHashNew = "newhash",
            ExecutableHashOld = "oldhash"
        };

        // Act
        var result = await _checker.CheckIntegrityAsync(config);

        // Assert
        result.HasIssues.Should().BeTrue();
        result.VersionStatus.Should().Be(ExecutableVersionStatus.Unknown);
        result.Issues.Should().Contain(i => i.Type == GameIntegrityIssueType.UnknownVersion);
    }

    [Fact]
    public async Task CheckIntegrityAsync_WithProgramFilesLocation_ReportsIssue()
    {
        // Arrange - use path that contains "Program Files" (won't actually exist)
        var config = new GameIntegrityConfiguration
        {
            GameType = GameType.Fallout4,
            GameDisplayName = "Fallout 4",
            GameRootPath = @"C:\Program Files\Steam\steamapps\common\Fallout 4",
            ExecutableHashNew = "hash"
        };

        // Act
        var result = await _checker.CheckIntegrityAsync(config);

        // Assert
        result.LocationStatus.Should().Be(InstallationLocationStatus.RestrictedLocation);
        result.Issues.Should().Contain(i => i.Type == GameIntegrityIssueType.RestrictedInstallLocation);
    }

    [Fact]
    public async Task CheckIntegrityAsync_WithSteamIni_ReportsIssue()
    {
        // Arrange
        var gameFolder = CreateGameFolder("Fallout 4");
        CreateFile("Fallout 4/Fallout4.exe", "exe content");
        CreateFile("Fallout 4/steam_api64.ini", "[Steam]\nAppId=377160");

        var config = new GameIntegrityConfiguration
        {
            GameType = GameType.Fallout4,
            GameDisplayName = "Fallout 4",
            GameRootPath = gameFolder,
            ExecutableHashNew = ComputeHash("exe content")
        };

        // Act
        var result = await _checker.CheckIntegrityAsync(config);

        // Assert
        result.SteamIniDetected.Should().BeTrue();
        result.HasIssues.Should().BeTrue();
        result.Issues.Should().Contain(i => i.Type == GameIntegrityIssueType.SteamIniPresent);
    }

    [Fact]
    public async Task CheckIntegrityAsync_WithMissingExecutable_ReportsIssue()
    {
        // Arrange
        var gameFolder = CreateGameFolder("Fallout 4");
        // Don't create the executable

        var config = new GameIntegrityConfiguration
        {
            GameType = GameType.Fallout4,
            GameDisplayName = "Fallout 4",
            GameRootPath = gameFolder,
            ExecutableHashNew = "hash"
        };

        // Act
        var result = await _checker.CheckIntegrityAsync(config);

        // Assert
        result.ExecutableFound.Should().BeFalse();
        result.VersionStatus.Should().Be(ExecutableVersionStatus.NotFound);
        result.HasIssues.Should().BeTrue();
        result.Issues.Should().Contain(i => i.Type == GameIntegrityIssueType.ExecutableNotFound);
    }

    [Fact]
    public async Task CheckIntegrityAsync_WithMultipleIssues_ReportsAll()
    {
        // Arrange - Program Files path + unknown version + Steam INI
        var gameFolder = CreateGameFolder("Program Files/Steam/Fallout 4");
        CreateFile("Program Files/Steam/Fallout 4/Fallout4.exe", "unknown content");
        CreateFile("Program Files/Steam/Fallout 4/steam_api64.ini", "[Steam]");

        var config = new GameIntegrityConfiguration
        {
            GameType = GameType.Fallout4,
            GameDisplayName = "Fallout 4",
            GameRootPath = gameFolder,
            ExecutableHashNew = "newhash",
            ExecutableHashOld = "oldhash"
        };

        // Act
        var result = await _checker.CheckIntegrityAsync(config);

        // Assert
        result.HasIssues.Should().BeTrue();
        result.Issues.Should().HaveCountGreaterThanOrEqualTo(3);
        result.Issues.Should().Contain(i => i.Type == GameIntegrityIssueType.RestrictedInstallLocation);
        result.Issues.Should().Contain(i => i.Type == GameIntegrityIssueType.UnknownVersion);
        result.Issues.Should().Contain(i => i.Type == GameIntegrityIssueType.SteamIniPresent);
    }

    [Fact]
    public async Task CheckIntegrityAsync_WithNoGameRootPath_HandlesGracefully()
    {
        // Arrange
        var config = new GameIntegrityConfiguration
        {
            GameType = GameType.Fallout4,
            GameDisplayName = "Fallout 4",
            GameRootPath = null
        };

        // Act
        var result = await _checker.CheckIntegrityAsync(config);

        // Assert
        result.ExecutablePath.Should().BeNull();
        result.VersionStatus.Should().Be(ExecutableVersionStatus.NotChecked);
        result.LocationStatus.Should().Be(InstallationLocationStatus.PathNotProvided);
    }

    [Fact]
    public async Task CheckIntegrityAsync_UsesGameTypeDisplayNameWhenConfigDisplayNameEmpty()
    {
        // Arrange
        var gameFolder = CreateGameFolder("Fallout 4");
        // Don't create executable to trigger ExecutableNotFound issue

        var config = new GameIntegrityConfiguration
        {
            GameType = GameType.Fallout4,
            GameDisplayName = string.Empty, // Empty display name
            GameRootPath = gameFolder,
            ExecutableHashNew = "hash"
        };

        // Act
        var result = await _checker.CheckIntegrityAsync(config);

        // Assert
        result.Issues.Should().Contain(i =>
            i.Type == GameIntegrityIssueType.ExecutableNotFound &&
            i.Message.Contains("Fallout 4")); // Should use GameType.GetDisplayName()
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task CheckIntegrityAsync_SupportsCancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var config = new GameIntegrityConfiguration
        {
            GameType = GameType.Fallout4
        };

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _checker.CheckIntegrityAsync(config, cts.Token));
    }

    [Fact]
    public async Task CheckExecutableVersionAsync_SupportsCancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _checker.CheckExecutableVersionAsync("path", "old", "new", cts.Token));
    }

    #endregion

    #region Hash Computation Tests

    [Fact]
    public async Task ComputeFileHashAsync_ReturnsLowercaseHex()
    {
        // Arrange
        var content = "test content for hashing";
        var filePath = CreateFile("test.bin", content);
        var expectedHash = ComputeHash(content);

        // Act
        var result = await _checker.ComputeFileHashAsync(filePath);

        // Assert
        result.Should().Be(expectedHash);
        result.Should().Be(result!.ToLowerInvariant()); // Verify lowercase
    }

    [Fact]
    public async Task ComputeFileHashAsync_WithMissingFile_ReturnsNull()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "nonexistent.bin");

        // Act
        var result = await _checker.ComputeFileHashAsync(filePath);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ComputeFileHashAsync_WithEmptyFile_ReturnsValidHash()
    {
        // Arrange
        var filePath = CreateFile("empty.bin", string.Empty);

        // Act
        var result = await _checker.ComputeFileHashAsync(filePath);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveLength(64); // SHA-256 produces 32 bytes = 64 hex chars
    }

    #endregion

    #region GameIntegrityResult HasIssues Tests

    [Fact]
    public void GameIntegrityResult_HasIssues_ReturnsTrueForOutdated()
    {
        var result = new GameIntegrityResult { VersionStatus = ExecutableVersionStatus.Outdated };
        result.HasIssues.Should().BeTrue();
    }

    [Fact]
    public void GameIntegrityResult_HasIssues_ReturnsTrueForUnknown()
    {
        var result = new GameIntegrityResult { VersionStatus = ExecutableVersionStatus.Unknown };
        result.HasIssues.Should().BeTrue();
    }

    [Fact]
    public void GameIntegrityResult_HasIssues_ReturnsTrueForNotFound()
    {
        var result = new GameIntegrityResult { VersionStatus = ExecutableVersionStatus.NotFound };
        result.HasIssues.Should().BeTrue();
    }

    [Fact]
    public void GameIntegrityResult_HasIssues_ReturnsTrueForHashError()
    {
        var result = new GameIntegrityResult { VersionStatus = ExecutableVersionStatus.HashError };
        result.HasIssues.Should().BeTrue();
    }

    [Fact]
    public void GameIntegrityResult_HasIssues_ReturnsTrueForRestrictedLocation()
    {
        var result = new GameIntegrityResult { LocationStatus = InstallationLocationStatus.RestrictedLocation };
        result.HasIssues.Should().BeTrue();
    }

    [Fact]
    public void GameIntegrityResult_HasIssues_ReturnsTrueForSteamIniDetected()
    {
        var result = new GameIntegrityResult { SteamIniDetected = true };
        result.HasIssues.Should().BeTrue();
    }

    [Fact]
    public void GameIntegrityResult_HasIssues_ReturnsFalseForHealthyResult()
    {
        var result = new GameIntegrityResult
        {
            VersionStatus = ExecutableVersionStatus.LatestVersion,
            LocationStatus = InstallationLocationStatus.RecommendedLocation,
            SteamIniDetected = false
        };
        result.HasIssues.Should().BeFalse();
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

    private string CreateGameFolder(string relativePath)
    {
        var fullPath = Path.Combine(_tempDirectory, relativePath);
        Directory.CreateDirectory(fullPath);
        return fullPath;
    }

    private static string ComputeHash(string content)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    #endregion
}
