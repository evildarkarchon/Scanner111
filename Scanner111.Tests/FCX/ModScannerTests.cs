using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Scanner111.Core.FCX;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Core.Models.Yaml;
using Scanner111.Tests.TestHelpers;
using Xunit;
using FluentAssertions;

namespace Scanner111.Tests.FCX;

public class ModScannerTests : IDisposable
{
    private readonly ModScanner _scanner;
    private readonly ILogger<ModScanner> _logger;
    private readonly IApplicationSettingsService _appSettings;
    private readonly IBackupService _backupService;
    private readonly IHashValidationService _hashService;
    private readonly TestYamlSettingsProvider _yamlSettings;
    private readonly string _testDirectory;
    private readonly ApplicationSettings _defaultSettings;

    public ModScannerTests()
    {
        var loggerMock = new Mock<ILogger<ModScanner>>();
        _logger = loggerMock.Object;
        var appSettingsMock = new Mock<IApplicationSettingsService>();
        _appSettings = appSettingsMock.Object;
        var backupServiceMock = new Mock<IBackupService>();
        _backupService = backupServiceMock.Object;
        var hashServiceMock = new Mock<IHashValidationService>();
        _hashService = hashServiceMock.Object;
        _yamlSettings = new TestYamlSettingsProvider();
        
        _testDirectory = Path.Combine(Path.GetTempPath(), "ModScannerTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_testDirectory);
        
        _defaultSettings = new ApplicationSettings
        {
            DefaultGamePath = @"C:\Games\Fallout4",
            BackupDirectory = Path.Combine(_testDirectory, "Backup"),
            MoveUnsolvedLogs = false
        };
        
        appSettingsMock.Setup(x => x.LoadSettingsAsync())
            .ReturnsAsync(_defaultSettings);
        
        _scanner = new ModScanner(
            _logger,
            _yamlSettings,
            _appSettings,
            _backupService,
            _hashService);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public async Task ScanAllModsAsync_WithValidPath_ScansUnpackedAndArchivedMods()
    {
        // Arrange
        var modPath = Path.Combine(_testDirectory, "Mods");
        Directory.CreateDirectory(modPath);
        
        // Create test files
        CreateTestFile(Path.Combine(modPath, "TestMod", "textures", "test.dds"), CreateValidDdsHeader());
        CreateTestFile(Path.Combine(modPath, "TestMod", "test.txt"), "readme content");
        // Create BA2 file with invalid format to avoid BSArch execution
        var invalidBa2Header = new byte[12];
        Encoding.ASCII.GetBytes("BTDX").CopyTo(invalidBa2Header, 0);
        Encoding.ASCII.GetBytes("TEST").CopyTo(invalidBa2Header, 8); // Invalid format type
        CreateTestFile(Path.Combine(modPath, "TestMod", "archive.ba2"), invalidBa2Header);
        
        var progress = new Progress<string>();
        var progressMessages = new List<string>();
        progress.ProgressChanged += (_, msg) => progressMessages.Add(msg);
        
        // Act
        var result = await _scanner.ScanAllModsAsync(modPath, progress);
        
        // Assert
        result.Should().NotBeNull("scan result should not be null");
        result.TotalFilesScanned.Should().BeGreaterThan(0, "files should have been scanned");
        result.TotalArchivesScanned.Should().BeGreaterThan(0, "archives should have been scanned");
        progressMessages.Should().Contain("Scanning unpacked mod files...", "unpacked scan progress should be reported");
        progressMessages.Should().Contain("Scanning archived mod files...", "archived scan progress should be reported");
    }

    [Fact]
    public async Task ScanAllModsAsync_WithInvalidPath_ReturnsEmptyResult()
    {
        // Arrange
        var invalidPath = Path.Combine(_testDirectory, "NonExistent");
        
        // Act
        var result = await _scanner.ScanAllModsAsync(invalidPath);
        
        // Assert
        result.Should().NotBeNull("scan result should not be null even for invalid path");
        result.TotalFilesScanned.Should().Be(0, "no files should be scanned for non-existent path");
        result.TotalArchivesScanned.Should().Be(0, "no archives should be scanned for non-existent path");
        result.Issues.Should().BeEmpty("no issues should be reported for non-existent path");
        // Verify logger was called for both unpacked and archived scans
        // Note: NSubstitute verification removed due to logger extension method limitations
    }

    [Fact]
    public async Task ScanUnpackedModsAsync_DetectsFomodFolders()
    {
        // Arrange
        var modPath = Path.Combine(_testDirectory, "Mods");
        var fomodPath = Path.Combine(modPath, "TestMod", "fomod");
        Directory.CreateDirectory(fomodPath);
        CreateTestFile(Path.Combine(fomodPath, "info.xml"), "<fomod/>");
        
        // Act
        var result = await _scanner.ScanUnpackedModsAsync(modPath);
        
        // Assert
        var fomodIssue = result.Issues.FirstOrDefault(i => i.Type == ModIssueType.CleanupFile);
        fomodIssue.Should().NotBeNull("FOMOD folder should be detected as a cleanup issue");
        fomodIssue.Description.Should().Contain("FOMod folder", "description should mention FOMOD");
        fomodIssue.FilePath.ToLower().Should().Contain("fomod", "file path should contain fomod");
    }

    [Fact]
    public async Task ScanUnpackedModsAsync_DetectsInvalidTextureFormats()
    {
        // Arrange
        var modPath = Path.Combine(_testDirectory, "Mods");
        CreateTestFile(Path.Combine(modPath, "TestMod", "textures", "test.png"), new byte[] { 0x89, 0x50, 0x4E, 0x47 });
        CreateTestFile(Path.Combine(modPath, "TestMod", "textures", "test.tga"), new byte[] { 0x00, 0x00, 0x02 });
        
        // Act
        var result = await _scanner.ScanUnpackedModsAsync(modPath);
        
        // Assert
        var textureIssues = result.Issues.Where(i => i.Type == ModIssueType.TextureFormatIncorrect).ToList();
        textureIssues.Should().HaveCount(2, "both PNG and TGA files should be detected");
        textureIssues.Should().Contain(i => i.Description.Contains("PNG"), "PNG format issue should be detected");
        textureIssues.Should().Contain(i => i.Description.Contains("TGA"), "TGA format issue should be detected");
    }

    [Fact]
    public async Task ScanUnpackedModsAsync_ValidatesInvalidDdsDimensions()
    {
        // Arrange
        var modPath = Path.Combine(_testDirectory, "Mods");
        var ddsData = CreateInvalidDdsDimensions(1023, 513); // Odd dimensions
        CreateTestFile(Path.Combine(modPath, "TestMod", "textures", "invalid.dds"), ddsData);
        
        // Act
        var result = await _scanner.ScanUnpackedModsAsync(modPath);
        
        // Assert
        var dimensionIssue = result.Issues.FirstOrDefault(i => i.Type == ModIssueType.TextureDimensionsInvalid);
        dimensionIssue.Should().NotBeNull("invalid DDS dimensions should be detected");
        dimensionIssue.Description.Should().Contain("not divisible by 2", "description should mention divisibility issue");
        dimensionIssue.AdditionalInfo.Should().Contain("1023x513", "actual dimensions should be reported");
    }

    [Fact]
    public async Task ScanUnpackedModsAsync_DetectsInvalidSoundFormats()
    {
        // Arrange
        var modPath = Path.Combine(_testDirectory, "Mods");
        CreateTestFile(Path.Combine(modPath, "TestMod", "sound", "music.mp3"), new byte[] { 0xFF, 0xFB });
        CreateTestFile(Path.Combine(modPath, "TestMod", "sound", "voice.m4a"), new byte[] { 0x00, 0x00, 0x00, 0x20 });
        
        // Act
        var result = await _scanner.ScanUnpackedModsAsync(modPath);
        
        // Assert
        var soundIssues = result.Issues.Where(i => i.Type == ModIssueType.SoundFormatIncorrect).ToList();
        soundIssues.Should().HaveCount(2, "both MP3 and M4A files should be detected");
        soundIssues.Should().Contain(i => i.Description.Contains("MP3"), "MP3 format issue should be detected");
        soundIssues.Should().Contain(i => i.Description.Contains("M4A"), "M4A format issue should be detected");
    }

    [Fact]
    public async Task ScanUnpackedModsAsync_DetectsAnimationData()
    {
        // Arrange
        var modPath = Path.Combine(_testDirectory, "Mods");
        var animPath = Path.Combine(modPath, "TestMod", "meshes", "animationfiledata");
        Directory.CreateDirectory(animPath);
        CreateTestFile(Path.Combine(animPath, "animation.hkx"), new byte[] { 0x01, 0x02, 0x03 });
        
        // Act
        var result = await _scanner.ScanUnpackedModsAsync(modPath);
        
        // Assert
        var animIssue = result.Issues.FirstOrDefault(i => i.Type == ModIssueType.AnimationData);
        animIssue.Should().NotBeNull("animation data should be detected");
        animIssue.Description.Should().Contain("custom animation file data", "description should mention animation data");
    }

    [Fact]
    public async Task ScanUnpackedModsAsync_DetectsPrevisFiles()
    {
        // Arrange
        var modPath = Path.Combine(_testDirectory, "Mods");
        CreateTestFile(Path.Combine(modPath, "TestMod", "meshes", "precombined.uvd"), new byte[] { 0x01 });
        CreateTestFile(Path.Combine(modPath, "TestMod", "meshes", "object_oc.nif"), new byte[] { 0x01 });
        
        // Act
        var result = await _scanner.ScanUnpackedModsAsync(modPath);
        
        // Assert
        var previsIssue = result.Issues.FirstOrDefault(i => i.Type == ModIssueType.PrevisFile);
        previsIssue.Should().NotBeNull("previs files should be detected");
        previsIssue.Description.Should().Contain("precombine/previs files", "description should mention precombine/previs");
    }

    [Fact]
    public async Task ScanUnpackedModsAsync_HandlesProgressReporting()
    {
        // Arrange
        var modPath = Path.Combine(_testDirectory, "Mods");
        // Create 101 files to trigger progress report
        for (int i = 0; i < 101; i++)
        {
            CreateTestFile(Path.Combine(modPath, $"file{i}.txt"), "content");
        }
        
        var progress = new Progress<string>();
        var progressMessages = new List<string>();
        progress.ProgressChanged += (_, msg) => progressMessages.Add(msg);
        
        // Act
        await _scanner.ScanUnpackedModsAsync(modPath, progress);
        
        // Assert
        progressMessages.Should().Contain(msg => msg.Contains("Analyzed") && msg.Contains("files"), "progress should report analyzed files");
    }

    [Fact]
    public async Task ScanUnpackedModsAsync_RespectsBodySlideExceptionForTextures()
    {
        // Arrange
        var modPath = Path.Combine(_testDirectory, "Mods");
        CreateTestFile(Path.Combine(modPath, "BodySlide", "textures", "preview.png"), new byte[] { 0x89, 0x50 });
        CreateTestFile(Path.Combine(modPath, "Regular", "textures", "texture.png"), new byte[] { 0x89, 0x50 });
        
        // Act
        var result = await _scanner.ScanUnpackedModsAsync(modPath);
        
        // Assert
        var textureIssues = result.Issues.Where(i => i.Type == ModIssueType.TextureFormatIncorrect).ToList();
        textureIssues.Should().ContainSingle("only non-BodySlide PNG should be flagged");
        textureIssues[0].FilePath.Should().Contain("Regular", "only the regular texture should be flagged");
    }

    [Fact]
    public async Task ScanArchivedModsAsync_ValidatesBa2Format()
    {
        // Arrange
        var modPath = Path.Combine(_testDirectory, "Mods");
        Directory.CreateDirectory(modPath);
        
        // Create invalid BA2 file - this will be detected as invalid format and won't trigger BSArch execution
        CreateTestFile(Path.Combine(modPath, "invalid.ba2"), Encoding.ASCII.GetBytes("INVA0000LIDT"));
        
        // No need to create BSArch.exe since invalid BA2 files won't trigger its execution
        
        // Act
        var result = await _scanner.ScanArchivedModsAsync(modPath);
        
        // Assert
        var formatIssue = result.Issues.FirstOrDefault(i => i.Type == ModIssueType.ArchiveFormatIncorrect);
        formatIssue.Should().NotBeNull("invalid BA2 format should be detected");
        formatIssue.Description.Should().Contain("incorrect format", "description should mention incorrect format");
    }

    [Fact]
    public async Task ScanArchivedModsAsync_SkipsPrpMainArchive()
    {
        // Arrange
        var modPath = Path.Combine(_testDirectory, "Mods");
        Directory.CreateDirectory(modPath);
        
        // Create BA2 files with invalid headers to avoid BSArch execution
        // Use a header that's recognized as BA2 but not valid for processing
        var invalidButRecognizedHeader = new byte[12];
        Encoding.ASCII.GetBytes("BTDX").CopyTo(invalidButRecognizedHeader, 0);
        Encoding.ASCII.GetBytes("INVL").CopyTo(invalidButRecognizedHeader, 8); // Invalid format type
        
        CreateTestFile(Path.Combine(modPath, "prp - main.ba2"), invalidButRecognizedHeader);
        CreateTestFile(Path.Combine(modPath, "regular.ba2"), invalidButRecognizedHeader);
        
        // Act
        var result = await _scanner.ScanArchivedModsAsync(modPath);
        
        // Assert
        result.TotalArchivesScanned.Should().Be(2, "both files should be counted as scanned");
        // Only regular.ba2 will have format issue since prp - main.ba2 is skipped before format check
        result.Issues.Count(i => i.Type == ModIssueType.ArchiveFormatIncorrect).Should().Be(1, "only regular.ba2 should have format issue");
    }

    [Fact]
    public async Task ScanAllModsAsync_HandlesIoCancellation()
    {
        // Arrange
        var modPath = Path.Combine(_testDirectory, "Mods");
        Directory.CreateDirectory(modPath);
        
        // Create many files to ensure cancellation can occur
        for (int i = 0; i < 1000; i++)
        {
            CreateTestFile(Path.Combine(modPath, $"file{i}.txt"), "content");
        }
        
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately
        
        // Act & Assert
        var action = () => _scanner.ScanAllModsAsync(modPath, null, cts.Token);
        await action.Should().ThrowAsync<OperationCanceledException>("scan should be cancelled when token is cancelled");
    }

    [Fact]
    public async Task CleanupModFilesAsync_RemovesDocumentationFiles()
    {
        // Arrange
        var modPath = Path.Combine(_testDirectory, "Mods");
        CreateTestFile(Path.Combine(modPath, "readme.txt"), "content");
        CreateTestFile(Path.Combine(modPath, "changelog.txt"), "content");
        CreateTestFile(Path.Combine(modPath, "important.txt"), "content"); // Should not be removed
        
        // Act
        var result = await _scanner.ScanUnpackedModsAsync(modPath);
        
        // Assert
        result.CleanedFiles.Should().HaveCount(2, "two documentation files should be cleaned");
        result.CleanedFiles.Should().Contain(f => f.Contains("readme.txt"), "readme should be cleaned");
        result.CleanedFiles.Should().Contain(f => f.Contains("changelog.txt"), "changelog should be cleaned");
        result.CleanedFiles.Should().NotContain(f => f.Contains("important.txt"), "important.txt should not be cleaned");
    }

    // Helper methods
    private void CreateTestFile(string path, byte[] content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
    }

    private void CreateTestFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private byte[] CreateValidDdsHeader()
    {
        var header = new byte[128];
        // DDS signature
        header[0] = (byte)'D';
        header[1] = (byte)'D';
        header[2] = (byte)'S';
        header[3] = (byte)' ';
        
        // Height at offset 12 (512)
        BitConverter.GetBytes(512u).CopyTo(header, 12);
        // Width at offset 16 (512)
        BitConverter.GetBytes(512u).CopyTo(header, 16);
        
        return header;
    }

    private byte[] CreateInvalidDdsDimensions(uint width, uint height)
    {
        var header = new byte[128];
        // DDS signature
        header[0] = (byte)'D';
        header[1] = (byte)'D';
        header[2] = (byte)'S';
        header[3] = (byte)' ';
        
        // Height at offset 12
        BitConverter.GetBytes(height).CopyTo(header, 12);
        // Width at offset 16
        BitConverter.GetBytes(width).CopyTo(header, 16);
        
        return header;
    }

    private byte[] CreateValidBa2Header()
    {
        var header = new byte[12];
        // BTDX signature
        header[0] = (byte)'B';
        header[1] = (byte)'T';
        header[2] = (byte)'D';
        header[3] = (byte)'X';
        
        // DX10 format at offset 8
        header[8] = (byte)'D';
        header[9] = (byte)'X';
        header[10] = (byte)'1';
        header[11] = (byte)'0';
        
        return header;
    }
}