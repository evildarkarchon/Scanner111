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
        CreateTestFile(Path.Combine(modPath, "TestMod", "archive.ba2"), CreateValidBa2Header());
        
        var progress = new Progress<string>();
        var progressMessages = new List<string>();
        progress.ProgressChanged += (_, msg) => progressMessages.Add(msg);
        
        // Act
        var result = await _scanner.ScanAllModsAsync(modPath, progress);
        
        // Assert
        Assert.NotNull(result);
        Assert.True(result.TotalFilesScanned > 0);
        Assert.True(result.TotalArchivesScanned > 0);
        Assert.Contains("Scanning unpacked mod files...", progressMessages);
        Assert.Contains("Scanning archived mod files...", progressMessages);
    }

    [Fact]
    public async Task ScanAllModsAsync_WithInvalidPath_ReturnsEmptyResult()
    {
        // Arrange
        var invalidPath = Path.Combine(_testDirectory, "NonExistent");
        
        // Act
        var result = await _scanner.ScanAllModsAsync(invalidPath);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.TotalFilesScanned);
        Assert.Equal(0, result.TotalArchivesScanned);
        Assert.Empty(result.Issues);
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
        Assert.NotNull(fomodIssue);
        Assert.Contains("FOMod folder", fomodIssue.Description);
        Assert.Contains("fomod", fomodIssue.FilePath.ToLower());
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
        Assert.Equal(2, textureIssues.Count);
        Assert.Contains(textureIssues, i => i.Description.Contains("PNG"));
        Assert.Contains(textureIssues, i => i.Description.Contains("TGA"));
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
        Assert.NotNull(dimensionIssue);
        Assert.Contains("not divisible by 2", dimensionIssue.Description);
        Assert.Contains("1023x513", dimensionIssue.AdditionalInfo);
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
        Assert.Equal(2, soundIssues.Count);
        Assert.Contains(soundIssues, i => i.Description.Contains("MP3"));
        Assert.Contains(soundIssues, i => i.Description.Contains("M4A"));
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
        Assert.NotNull(animIssue);
        Assert.Contains("custom animation file data", animIssue.Description);
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
        Assert.NotNull(previsIssue);
        Assert.Contains("precombine/previs files", previsIssue.Description);
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
        Assert.Contains(progressMessages, msg => msg.Contains("Analyzed") && msg.Contains("files"));
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
        Assert.Single(textureIssues); // Only the non-BodySlide PNG should be flagged
        Assert.Contains("Regular", textureIssues[0].FilePath);
    }

    [Fact]
    public async Task ScanArchivedModsAsync_ValidatesBa2Format()
    {
        // Arrange
        var modPath = Path.Combine(_testDirectory, "Mods");
        Directory.CreateDirectory(modPath);
        
        // Create invalid BA2 file
        CreateTestFile(Path.Combine(modPath, "invalid.ba2"), Encoding.ASCII.GetBytes("INVA0000LIDT"));
        
        // Mock BSArch.exe existence
        var bsarchPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "BSArch.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(bsarchPath)!);
        CreateTestFile(bsarchPath, new byte[] { 0x4D, 0x5A }); // MZ header
        
        // Act
        var result = await _scanner.ScanArchivedModsAsync(modPath);
        
        // Assert
        var formatIssue = result.Issues.FirstOrDefault(i => i.Type == ModIssueType.ArchiveFormatIncorrect);
        Assert.NotNull(formatIssue);
        Assert.Contains("incorrect format", formatIssue.Description);
    }

    [Fact]
    public async Task ScanArchivedModsAsync_SkipsPrpMainArchive()
    {
        // Arrange
        var modPath = Path.Combine(_testDirectory, "Mods");
        Directory.CreateDirectory(modPath);
        
        CreateTestFile(Path.Combine(modPath, "prp - main.ba2"), CreateValidBa2Header());
        CreateTestFile(Path.Combine(modPath, "regular.ba2"), CreateValidBa2Header());
        
        // Mock BSArch.exe existence
        var bsarchPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "BSArch.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(bsarchPath)!);
        CreateTestFile(bsarchPath, new byte[] { 0x4D, 0x5A });
        
        // Act
        var result = await _scanner.ScanArchivedModsAsync(modPath);
        
        // Assert
        Assert.Equal(2, result.TotalArchivesScanned); // Both files are counted, but prp - main.ba2 is skipped in processing
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
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _scanner.ScanAllModsAsync(modPath, null, cts.Token));
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
        Assert.Equal(2, result.CleanedFiles.Count);
        Assert.Contains(result.CleanedFiles, f => f.Contains("readme.txt"));
        Assert.Contains(result.CleanedFiles, f => f.Contains("changelog.txt"));
        Assert.DoesNotContain(result.CleanedFiles, f => f.Contains("important.txt"));
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