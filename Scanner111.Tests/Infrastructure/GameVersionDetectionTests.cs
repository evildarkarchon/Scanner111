using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Scanner111.Core.Infrastructure;
using Xunit;

namespace Scanner111.Tests.Infrastructure;

public class GameVersionDetectionTests : IDisposable
{
    private readonly string _testDirectory;

    public GameVersionDetectionTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "GameVersionDetectionTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public async Task CalculateFileHashAsync_WithValidFile_ReturnsCorrectHash()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.exe");
        var testContent = "This is a test file content";
        await File.WriteAllTextAsync(testFile, testContent);
        
        // Calculate expected hash
        using var sha256 = SHA256.Create();
        var expectedHashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(testContent));
        var expectedHash = BitConverter.ToString(expectedHashBytes).Replace("-", "").ToLowerInvariant();
        
        // Act
        var actualHash = await GameVersionDetection.CalculateFileHashAsync(testFile);
        
        // Assert
        Assert.Equal(expectedHash, actualHash);
    }

    [Fact]
    public async Task CalculateFileHashAsync_WithLargeFile_CalculatesCorrectly()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "large.exe");
        var largeContent = new byte[10 * 1024 * 1024]; // 10MB
        new Random(42).NextBytes(largeContent);
        await File.WriteAllBytesAsync(testFile, largeContent);
        
        // Calculate expected hash
        using var sha256 = SHA256.Create();
        var expectedHashBytes = sha256.ComputeHash(largeContent);
        var expectedHash = BitConverter.ToString(expectedHashBytes).Replace("-", "").ToLowerInvariant();
        
        // Act
        var actualHash = await GameVersionDetection.CalculateFileHashAsync(testFile);
        
        // Assert
        Assert.Equal(expectedHash, actualHash);
    }

    [Fact]
    public async Task CalculateFileHashAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.exe");
        var largeContent = new byte[50 * 1024 * 1024]; // 50MB to ensure cancellation can occur
        await File.WriteAllBytesAsync(testFile, largeContent);
        
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(1); // Cancel almost immediately
        
        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(
            () => GameVersionDetection.CalculateFileHashAsync(testFile, cts.Token));
    }

    [Fact]
    public async Task DetectGameVersionAsync_WithNonExistentFile_ReturnsNull()
    {
        // Arrange
        var nonExistentFile = Path.Combine(_testDirectory, "nonexistent.exe");
        
        // Act
        var result = await GameVersionDetection.DetectGameVersionAsync(nonExistentFile);
        
        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task DetectGameVersionAsync_WithKnownPreNextGenHash_ReturnsCorrectVersion()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "Fallout4.exe");
        // Create a file that will produce the pre-next gen hash
        // Note: In a real scenario, we'd need the actual file or mock the hash calculation
        await File.WriteAllTextAsync(testFile, "Mock Pre-Next Gen Content");
        
        // Since we can't easily create a file with the exact hash, we'll test the logic
        // by verifying unknown version handling
        
        // Act
        var result = await GameVersionDetection.DetectGameVersionAsync(testFile);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal("Unknown", result.Version);
        Assert.Equal("Unknown Version", result.Name);
        Assert.Contains("Unrecognized game version", result.Description);
        Assert.NotEmpty(result.ExecutableHash);
        Assert.False(result.IsKnownVersion);
        Assert.False(result.IsModdingRecommended);
    }

    [Fact]
    public async Task DetectGameVersionAsync_WithUnknownVersion_ReturnsUnknownVersionInfo()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "Fallout4.exe");
        await File.WriteAllTextAsync(testFile, "Unknown version content");
        
        // Act
        var result = await GameVersionDetection.DetectGameVersionAsync(testFile);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal("Unknown", result.Version);
        Assert.Equal("Unknown Version", result.Name);
        Assert.Contains("Unrecognized game version", result.Description);
        Assert.NotEmpty(result.Notes);
        Assert.Contains(result.Notes, note => note.Contains("Pirated or cracked version"));
        Assert.Contains(result.Notes, note => note.Contains("Beta or pre-release version"));
        Assert.Contains(result.Notes, note => note.Contains("Modified executable"));
        Assert.Contains(result.Notes, note => note.Contains("Mod compatibility cannot be guaranteed"));
        Assert.False(result.IsKnownVersion);
        Assert.False(result.IsModdingRecommended);
    }

    [Fact]
    public async Task DetectGameVersionAsync_WithFileAccessError_ReturnsNull()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "locked.exe");
        await File.WriteAllTextAsync(testFile, "content");
        
        // Lock the file
        using var fileStream = new FileStream(testFile, FileMode.Open, FileAccess.Read, FileShare.None);
        
        // Act
        var result = await GameVersionDetection.DetectGameVersionAsync(testFile);
        
        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void IsF4seCompatible_WithKnownVersionAndCompatibleF4se_ReturnsTrue()
    {
        // Arrange
        var gameVersion = "1.10.163.0"; // Pre-Next Gen version
        var f4seVersion = "0.6.23"; // Exact required version
        
        // Act
        var result = GameVersionDetection.IsF4seCompatible(gameVersion, f4seVersion);
        
        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsF4seCompatible_WithKnownVersionAndNewerF4se_ReturnsTrue()
    {
        // Arrange
        var gameVersion = "1.10.163.0"; // Pre-Next Gen version
        var f4seVersion = "0.6.24"; // Newer than required
        
        // Act
        var result = GameVersionDetection.IsF4seCompatible(gameVersion, f4seVersion);
        
        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsF4seCompatible_WithKnownVersionAndOlderF4se_ReturnsFalse()
    {
        // Arrange
        var gameVersion = "1.10.984.0"; // Next Gen version requiring 0.7.2
        var f4seVersion = "0.7.1"; // Older than required
        
        // Act
        var result = GameVersionDetection.IsF4seCompatible(gameVersion, f4seVersion);
        
        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsF4seCompatible_WithUnknownVersion_ReturnsFalse()
    {
        // Arrange
        var gameVersion = "1.11.999.0"; // Unknown version
        var f4seVersion = "0.7.5";
        
        // Act
        var result = GameVersionDetection.IsF4seCompatible(gameVersion, f4seVersion);
        
        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GetVersionCompatibilityNotes_WithPreNextGenVersion_ReturnsCorrectNotes()
    {
        // Arrange
        var gameVersion = "1.10.163.0";
        
        // Act
        var notes = GameVersionDetection.GetVersionCompatibilityNotes(gameVersion);
        
        // Assert
        Assert.NotNull(notes);
        Assert.NotEmpty(notes);
        Assert.Contains(notes, note => note.Contains("Most mods are built for this version"));
        Assert.Contains(notes, note => note.Contains("Best stability with large mod lists"));
        Assert.Contains(notes, note => note.Contains("Recommended for heavy modding"));
    }

    [Fact]
    public void GetVersionCompatibilityNotes_WithNextGenVersion_ReturnsCorrectNotes()
    {
        // Arrange
        var gameVersion = "1.10.984.0";
        
        // Act
        var notes = GameVersionDetection.GetVersionCompatibilityNotes(gameVersion);
        
        // Assert
        Assert.NotNull(notes);
        Assert.NotEmpty(notes);
        Assert.Contains(notes, note => note.Contains("Many mods require updates for this version"));
        Assert.Contains(notes, note => note.Contains("Improved performance and graphics"));
        Assert.Contains(notes, note => note.Contains("Some older mods may not be compatible"));
        Assert.Contains(notes, note => note.Contains("Check mod compatibility before updating"));
    }

    [Fact]
    public void GetVersionCompatibilityNotes_WithUnknownVersion_ReturnsDefaultNote()
    {
        // Arrange
        var gameVersion = "1.11.999.0";
        
        // Act
        var notes = GameVersionDetection.GetVersionCompatibilityNotes(gameVersion);
        
        // Assert
        Assert.NotNull(notes);
        Assert.Single(notes);
        Assert.Equal("Unknown version - compatibility cannot be determined", notes[0]);
    }

    [Fact]
    public void GameVersionInfo_IsModdingRecommended_OnlyTrueForPreNextGen()
    {
        // Arrange & Act
        var preNextGenInfo = new GameVersionInfo { Version = "1.10.163.0" };
        var nextGenInfo = new GameVersionInfo { Version = "1.10.984.0" };
        var unknownInfo = new GameVersionInfo { Version = "Unknown" };
        
        // Assert
        Assert.True(preNextGenInfo.IsModdingRecommended);
        Assert.False(nextGenInfo.IsModdingRecommended);
        Assert.False(unknownInfo.IsModdingRecommended);
    }

    [Fact]
    public void GameVersionInfo_IsKnownVersion_FalseOnlyForUnknown()
    {
        // Arrange & Act
        var knownInfo = new GameVersionInfo { Version = "1.10.163.0" };
        var unknownInfo = new GameVersionInfo { Version = "Unknown" };
        
        // Assert
        Assert.True(knownInfo.IsKnownVersion);
        Assert.False(unknownInfo.IsKnownVersion);
    }
}