using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Scanner111.Core.Configuration;
using Scanner111.Core.Services;
using Xunit;

namespace Scanner111.Test.Services;

public sealed class ModFileScannerTests : IAsyncDisposable
{
    private readonly ILogger<ModFileScanner> _logger;
    private readonly IAsyncYamlSettingsCore _yamlCore;
    private readonly ModFileScanner _scanner;
    private readonly string _tempDirectory;

    public ModFileScannerTests()
    {
        _logger = Substitute.For<ILogger<ModFileScanner>>();
        _yamlCore = Substitute.For<IAsyncYamlSettingsCore>();
        _scanner = new ModFileScanner(_logger, _yamlCore);
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"ModFileScannerTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public async Task ScanModsUnpackedAsync_WithNonExistentPath_ReturnsErrorReport()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_tempDirectory, "nonexistent");

        // Act
        var result = await _scanner.ScanModsUnpackedAsync(nonExistentPath);

        // Assert
        result.Should().Contain("Mods folder not found");
        result.Should().Contain("❌");
    }

    [Fact]
    public async Task ScanModsUnpackedAsync_WithEmptyDirectory_ReturnsSuccessReport()
    {
        // Arrange
        var emptyModsPath = Path.Combine(_tempDirectory, "empty_mods");
        Directory.CreateDirectory(emptyModsPath);

        // Act
        var result = await _scanner.ScanModsUnpackedAsync(emptyModsPath);

        // Assert
        result.Should().Contain("MOD FILES SCAN");
        result.Should().Contain("UNPACKED");
        result.Should().NotContain("❌");
    }

    [Fact]
    public async Task ScanModsUnpackedAsync_WithBadDdsTexture_DetectsIssue()
    {
        // Arrange
        var modsPath = Path.Combine(_tempDirectory, "mods_with_bad_dds");
        var texturePath = Path.Combine(modsPath, "TestMod", "Textures");
        Directory.CreateDirectory(texturePath);

        // Create a mock DDS file with bad dimensions (odd numbers)
        var ddsFile = Path.Combine(texturePath, "badtexture.dds");
        await CreateMockDdsFileAsync(ddsFile, width: 511, height: 255); // Both odd numbers

        // Act
        var result = await _scanner.ScanModsUnpackedAsync(modsPath);

        // Assert
        result.Should().Contain("DDS DIMENSIONS ARE NOT DIVISIBLE BY 2");
        result.Should().Contain("⚠️");
        result.Should().Contain("badtexture.dds");
    }

    [Fact]
    public async Task ScanModsUnpackedAsync_WithGoodDdsTexture_DoesNotDetectDimensionIssue()
    {
        // Arrange
        var modsPath = Path.Combine(_tempDirectory, "mods_with_good_dds");
        var texturePath = Path.Combine(modsPath, "TestMod", "Textures");
        Directory.CreateDirectory(texturePath);

        // Create a mock DDS file with good dimensions (even numbers)
        var ddsFile = Path.Combine(texturePath, "goodtexture.dds");
        await CreateMockDdsFileAsync(ddsFile, width: 512, height: 256); // Both even numbers

        // Act
        var result = await _scanner.ScanModsUnpackedAsync(modsPath);

        // Assert
        result.Should().NotContain("DDS DIMENSIONS ARE NOT DIVISIBLE BY 2");
        result.Should().Contain("MOD FILES SCAN");
    }

    [Fact]
    public async Task ScanModsUnpackedAsync_WithIncorrectTextureFormat_DetectsIssue()
    {
        // Arrange
        var modsPath = Path.Combine(_tempDirectory, "mods_with_bad_format");
        var texturePath = Path.Combine(modsPath, "TestMod", "Textures");
        Directory.CreateDirectory(texturePath);

        // Create TGA file (should be DDS)
        var tgaFile = Path.Combine(texturePath, "texture.tga");
        await File.WriteAllTextAsync(tgaFile, "mock tga content");

        // Act
        var result = await _scanner.ScanModsUnpackedAsync(modsPath);

        // Assert
        result.Should().Contain("TEXTURE FILES HAVE INCORRECT FORMAT");
        result.Should().Contain("texture.tga");
        result.Should().Contain("TGA");
    }

    [Fact]
    public async Task ScanModsUnpackedAsync_WithIncorrectSoundFormat_DetectsIssue()
    {
        // Arrange
        var modsPath = Path.Combine(_tempDirectory, "mods_with_bad_sound");
        var soundPath = Path.Combine(modsPath, "TestMod", "Sound");
        Directory.CreateDirectory(soundPath);

        // Create MP3 file (should be XWM or WAV)
        var mp3File = Path.Combine(soundPath, "sound.mp3");
        await File.WriteAllTextAsync(mp3File, "mock mp3 content");

        // Act
        var result = await _scanner.ScanModsUnpackedAsync(modsPath);

        // Assert
        result.Should().Contain("SOUND FILES HAVE INCORRECT FORMAT");
        result.Should().Contain("sound.mp3");
        result.Should().Contain("MP3");
    }

    [Fact]
    public async Task ScanModsUnpackedAsync_WithDocumentationFile_DetectsCleanup()
    {
        // Arrange
        var modsPath = Path.Combine(_tempDirectory, "mods_with_docs");
        var modPath = Path.Combine(modsPath, "TestMod");
        Directory.CreateDirectory(modPath);

        // Create documentation file
        var readmeFile = Path.Combine(modPath, "readme.txt");
        await File.WriteAllTextAsync(readmeFile, "This is a readme file");

        // Act
        var result = await _scanner.ScanModsUnpackedAsync(modsPath);

        // Assert
        result.Should().Contain("DOCUMENTATION FILES MOVED");
        result.Should().Contain("readme.txt");
    }

    [Fact]
    public async Task ScanModsUnpackedAsync_WithAnimationDataFolder_DetectsIssue()
    {
        // Arrange
        var modsPath = Path.Combine(_tempDirectory, "mods_with_animdata");
        var animDataPath = Path.Combine(modsPath, "TestMod", "AnimationFileData");
        Directory.CreateDirectory(animDataPath);

        // Create a file in the animation data folder
        var animFile = Path.Combine(animDataPath, "test.hkx");
        await File.WriteAllTextAsync(animFile, "mock animation data");

        // Act
        var result = await _scanner.ScanModsUnpackedAsync(modsPath);

        // Assert
        result.Should().Contain("CUSTOM ANIMATION FILE DATA");
        result.Should().Contain("TestMod");
    }

    [Fact]
    public async Task ScanModsArchivedAsync_WithNonExistentBSArch_ReturnsErrorReport()
    {
        // Arrange
        var modsPath = Path.Combine(_tempDirectory, "mods");
        Directory.CreateDirectory(modsPath);
        var nonExistentBSArch = Path.Combine(_tempDirectory, "nonexistent_bsarch.exe");

        // Act
        var result = await _scanner.ScanModsArchivedAsync(modsPath, nonExistentBSArch);

        // Assert
        result.Should().Contain("BSArch.exe not found");
        result.Should().Contain("❌");
    }

    [Fact]
    public async Task CheckLogErrorsAsync_WithNonExistentPath_ReturnsErrorReport()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_tempDirectory, "nonexistent_logs");

        // Act
        var result = await _scanner.CheckLogErrorsAsync(nonExistentPath);

        // Assert
        result.Should().Contain("Log folder not found");
        result.Should().Contain("❌");
    }

    [Fact]
    public async Task CheckLogErrorsAsync_WithEmptyLogFolder_ReturnsSuccessReport()
    {
        // Arrange
        var logsPath = Path.Combine(_tempDirectory, "empty_logs");
        Directory.CreateDirectory(logsPath);

        // Act
        var result = await _scanner.CheckLogErrorsAsync(logsPath);

        // Assert
        result.Should().Contain("MOD FILES SCAN");
        result.Should().Contain("LOG_ERRORS");
        result.Should().NotContain("❌");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        var act = () => new ModFileScanner(null!, _yamlCore);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullYamlCore_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        var act = () => new ModFileScanner(_logger, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("yamlCore");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task ScanModsUnpackedAsync_WithInvalidPath_ThrowsArgumentException(string? invalidPath)
    {
        // Arrange & Act & Assert
        var act = () => _scanner.ScanModsUnpackedAsync(invalidPath!);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ScanModsUnpackedAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var modsPath = Path.Combine(_tempDirectory, "mods");
        Directory.CreateDirectory(modsPath);
        
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert
        var act = () => _scanner.ScanModsUnpackedAsync(modsPath, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private async Task CreateMockDdsFileAsync(string filePath, int width, int height)
    {
        // Create a minimal DDS header with specified dimensions
        var header = new byte[20];
        
        // DDS signature
        header[0] = 0x44; // 'D'
        header[1] = 0x44; // 'D' 
        header[2] = 0x53; // 'S'
        header[3] = 0x20; // ' '
        
        // Skip header size and flags (bytes 4-11)
        
        // Width (little-endian at offset 12)
        var widthBytes = BitConverter.GetBytes(width);
        Array.Copy(widthBytes, 0, header, 12, 4);
        
        // Height (little-endian at offset 16)
        var heightBytes = BitConverter.GetBytes(height);
        Array.Copy(heightBytes, 0, header, 16, 4);

        await File.WriteAllBytesAsync(filePath, header);
    }

    public async ValueTask DisposeAsync()
    {
        if (_scanner != null)
        {
            await _scanner.DisposeAsync().ConfigureAwait(false);
        }
        
        if (Directory.Exists(_tempDirectory))
        {
            try
            {
                Directory.Delete(_tempDirectory, true);
            }
            catch (Exception ex)
            {
                // Log but don't fail test cleanup
                Console.WriteLine($"Failed to cleanup temp directory: {ex.Message}");
            }
        }
    }
}