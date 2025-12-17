using FluentAssertions;
using Moq;
using Scanner111.Common.Models.ScanGame;
using Scanner111.Common.Services.ScanGame;

namespace Scanner111.Common.Tests.Services.ScanGame;

/// <summary>
/// Tests for UnpackedModsScanner and related components.
/// </summary>
public class UnpackedModsScannerTests : IDisposable
{
    private readonly UnpackedModsScanner _scanner;
    private readonly Mock<IDDSAnalyzer> _mockDdsAnalyzer;
    private readonly string _tempDirectory;

    public UnpackedModsScannerTests()
    {
        _mockDdsAnalyzer = new Mock<IDDSAnalyzer>();
        _scanner = new UnpackedModsScanner(_mockDdsAnalyzer.Object);
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"UnpackedModsScannerTests_{Guid.NewGuid():N}");
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

    #region Basic Scan Tests

    [Fact]
    public async Task ScanAsync_WithEmptyDirectory_ReturnsZeroCounts()
    {
        // Act
        var result = await _scanner.ScanAsync(_tempDirectory);

        // Assert
        result.TotalDirectoriesScanned.Should().Be(1); // Root directory
        result.TotalFilesScanned.Should().Be(0);
        result.HasIssues.Should().BeFalse();
    }

    [Fact]
    public async Task ScanAsync_WithNonExistentDirectory_ReturnsZeroCounts()
    {
        // Act
        var result = await _scanner.ScanAsync(Path.Combine(_tempDirectory, "nonexistent"));

        // Assert
        result.TotalDirectoriesScanned.Should().Be(0);
        result.TotalFilesScanned.Should().Be(0);
        result.HasIssues.Should().BeFalse();
    }

    [Fact]
    public async Task ScanAsync_WithNullOrEmptyPath_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _scanner.ScanAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => _scanner.ScanAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => _scanner.ScanAsync("   "));
    }

    [Fact]
    public async Task ScanAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(_tempDirectory, "test.txt"), "content");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _scanner.ScanAsync(_tempDirectory, cancellationToken: cts.Token));
    }

    #endregion

    #region Cleanup Issue Tests

    [Fact]
    public async Task ScanAsync_WithReadmeFile_ReportsCleanupIssue()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(_tempDirectory, "readme.txt"), "content");

        // Act
        var result = await _scanner.ScanAsync(_tempDirectory);

        // Assert
        result.HasIssues.Should().BeTrue();
        result.CleanupIssues.Should().HaveCount(1);
        result.CleanupIssues[0].ItemType.Should().Be(CleanupItemType.ReadmeFile);
        result.CleanupIssues[0].RelativePath.Should().Be("readme.txt");
    }

    [Theory]
    [InlineData("README.txt")]
    [InlineData("Readme.TXT")]
    [InlineData("changes.txt")]
    [InlineData("CHANGES.txt")]
    [InlineData("changelog.txt")]
    [InlineData("ChangeLog.txt")]
    [InlineData("change log.txt")]
    public async Task ScanAsync_WithVariousCleanupFiles_ReportsAllAsCleanupIssues(string fileName)
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(_tempDirectory, fileName), "content");

        // Act
        var result = await _scanner.ScanAsync(_tempDirectory);

        // Assert
        result.CleanupIssues.Should().HaveCount(1);
        result.CleanupIssues[0].ItemType.Should().Be(CleanupItemType.ReadmeFile);
    }

    [Fact]
    public async Task ScanAsync_WithFomodFolder_ReportsCleanupIssue()
    {
        // Arrange
        var fomodDir = Path.Combine(_tempDirectory, "fomod");
        Directory.CreateDirectory(fomodDir);
        await File.WriteAllTextAsync(Path.Combine(fomodDir, "info.xml"), "<config/>");

        // Act
        var result = await _scanner.ScanAsync(_tempDirectory);

        // Assert
        result.HasIssues.Should().BeTrue();
        result.CleanupIssues.Should().Contain(c =>
            c.ItemType == CleanupItemType.FomodFolder &&
            c.RelativePath.Contains("fomod", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ScanAsync_WithFomodFolderCaseInsensitive_ReportsCleanupIssue()
    {
        // Arrange
        var fomodDir = Path.Combine(_tempDirectory, "FOMOD");
        Directory.CreateDirectory(fomodDir);

        // Act
        var result = await _scanner.ScanAsync(_tempDirectory);

        // Assert
        result.CleanupIssues.Should().Contain(c => c.ItemType == CleanupItemType.FomodFolder);
    }

    [Fact]
    public async Task ScanAsync_WithNonCleanupTextFile_DoesNotReportAsCleanup()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(_tempDirectory, "config.txt"), "setting=value");

        // Act
        var result = await _scanner.ScanAsync(_tempDirectory);

        // Assert
        result.CleanupIssues.Should().BeEmpty();
    }

    #endregion

    #region Animation Data Tests

    [Fact]
    public async Task ScanAsync_WithAnimationFileDataDirectory_ReportsAnimationDataIssue()
    {
        // Arrange
        var animDir = Path.Combine(_tempDirectory, "meshes", "AnimationFileData");
        Directory.CreateDirectory(animDir);
        await File.WriteAllTextAsync(Path.Combine(animDir, "anim.hkx"), "data");

        // Act
        var result = await _scanner.ScanAsync(_tempDirectory);

        // Assert
        result.HasIssues.Should().BeTrue();
        result.AnimationDataIssues.Should().HaveCount(1);
    }

    [Fact]
    public async Task ScanAsync_WithAnimationDataDirCaseInsensitive_ReportsIssue()
    {
        // Arrange
        var animDir = Path.Combine(_tempDirectory, "meshes", "animationfiledata");
        Directory.CreateDirectory(animDir);

        // Act
        var result = await _scanner.ScanAsync(_tempDirectory);

        // Assert
        result.AnimationDataIssues.Should().HaveCount(1);
    }

    #endregion

    #region Texture Format Tests

    [Fact]
    public async Task ScanAsync_WithTgaFile_ReportsTextureFormatIssue()
    {
        // Arrange
        var textureDir = Path.Combine(_tempDirectory, "textures");
        Directory.CreateDirectory(textureDir);
        await File.WriteAllBytesAsync(Path.Combine(textureDir, "texture.tga"), new byte[] { 0, 0 });

        // Act
        var result = await _scanner.ScanAsync(_tempDirectory);

        // Assert
        result.HasIssues.Should().BeTrue();
        result.TextureFormatIssues.Should().HaveCount(1);
        result.TextureFormatIssues[0].Extension.Should().Be("TGA");
    }

    [Fact]
    public async Task ScanAsync_WithPngFile_ReportsTextureFormatIssue()
    {
        // Arrange
        var textureDir = Path.Combine(_tempDirectory, "textures");
        Directory.CreateDirectory(textureDir);
        await File.WriteAllBytesAsync(Path.Combine(textureDir, "texture.png"), new byte[] { 0x89, 0x50 });

        // Act
        var result = await _scanner.ScanAsync(_tempDirectory);

        // Assert
        result.TextureFormatIssues.Should().HaveCount(1);
        result.TextureFormatIssues[0].Extension.Should().Be("PNG");
    }

    [Fact]
    public async Task ScanAsync_WithTgaInBodySlide_DoesNotReportTextureFormatIssue()
    {
        // Arrange
        var bodySlideDir = Path.Combine(_tempDirectory, "CalienteTools", "BodySlide", "SliderSets");
        Directory.CreateDirectory(bodySlideDir);
        await File.WriteAllBytesAsync(Path.Combine(bodySlideDir, "preview.tga"), new byte[] { 0, 0 });

        // Act
        var result = await _scanner.ScanAsync(_tempDirectory);

        // Assert
        result.TextureFormatIssues.Should().BeEmpty();
    }

    #endregion

    #region Sound Format Tests

    [Fact]
    public async Task ScanAsync_WithMp3File_ReportsSoundFormatIssue()
    {
        // Arrange
        var soundDir = Path.Combine(_tempDirectory, "sound", "fx");
        Directory.CreateDirectory(soundDir);
        await File.WriteAllBytesAsync(Path.Combine(soundDir, "sound.mp3"), new byte[] { 0xFF, 0xFB });

        // Act
        var result = await _scanner.ScanAsync(_tempDirectory);

        // Assert
        result.HasIssues.Should().BeTrue();
        result.SoundFormatIssues.Should().HaveCount(1);
        result.SoundFormatIssues[0].Extension.Should().Be("MP3");
    }

    [Fact]
    public async Task ScanAsync_WithM4aFile_ReportsSoundFormatIssue()
    {
        // Arrange
        var soundDir = Path.Combine(_tempDirectory, "sound");
        Directory.CreateDirectory(soundDir);
        await File.WriteAllBytesAsync(Path.Combine(soundDir, "sound.m4a"), new byte[] { 0x00, 0x00 });

        // Act
        var result = await _scanner.ScanAsync(_tempDirectory);

        // Assert
        result.SoundFormatIssues.Should().HaveCount(1);
        result.SoundFormatIssues[0].Extension.Should().Be("M4A");
    }

    #endregion

    #region XSE Script Tests

    [Fact]
    public async Task ScanAsync_WithXseScriptFile_ReportsXseFileIssue()
    {
        // Arrange
        var scriptsDir = Path.Combine(_tempDirectory, "Data", "Scripts");
        Directory.CreateDirectory(scriptsDir);
        await File.WriteAllBytesAsync(Path.Combine(scriptsDir, "f4se.dll"), new byte[] { 0x4D, 0x5A });

        var xseScriptFiles = new Dictionary<string, string>
        {
            ["f4se.dll"] = "F4SE"
        };

        // Act
        var result = await _scanner.ScanAsync(_tempDirectory, xseScriptFiles);

        // Assert
        result.HasIssues.Should().BeTrue();
        result.XseFileIssues.Should().HaveCount(1);
    }

    [Fact]
    public async Task ScanAsync_WithXseScriptInWorkshopFramework_DoesNotReportIssue()
    {
        // Arrange
        var scriptsDir = Path.Combine(_tempDirectory, "Workshop Framework", "Scripts");
        Directory.CreateDirectory(scriptsDir);
        await File.WriteAllBytesAsync(Path.Combine(scriptsDir, "f4se.dll"), new byte[] { 0x4D, 0x5A });

        var xseScriptFiles = new Dictionary<string, string>
        {
            ["f4se.dll"] = "F4SE"
        };

        // Act
        var result = await _scanner.ScanAsync(_tempDirectory, xseScriptFiles);

        // Assert
        result.XseFileIssues.Should().BeEmpty();
    }

    [Fact]
    public async Task ScanAsync_WithXseScriptNotInScriptsDir_DoesNotReportIssue()
    {
        // Arrange
        var dataDir = Path.Combine(_tempDirectory, "Data");
        Directory.CreateDirectory(dataDir);
        await File.WriteAllBytesAsync(Path.Combine(dataDir, "f4se.dll"), new byte[] { 0x4D, 0x5A });

        var xseScriptFiles = new Dictionary<string, string>
        {
            ["f4se.dll"] = "F4SE"
        };

        // Act
        var result = await _scanner.ScanAsync(_tempDirectory, xseScriptFiles);

        // Assert
        result.XseFileIssues.Should().BeEmpty();
    }

    [Fact]
    public async Task ScanAsync_WithNoXseScriptsDictionary_DoesNotReportXseIssues()
    {
        // Arrange
        var scriptsDir = Path.Combine(_tempDirectory, "Scripts");
        Directory.CreateDirectory(scriptsDir);
        await File.WriteAllBytesAsync(Path.Combine(scriptsDir, "f4se.dll"), new byte[] { 0x4D, 0x5A });

        // Act - no xseScriptFiles dictionary provided
        var result = await _scanner.ScanAsync(_tempDirectory);

        // Assert
        result.XseFileIssues.Should().BeEmpty();
    }

    #endregion

    #region Previs File Tests

    [Fact]
    public async Task ScanAsync_WithUvdFile_ReportsPrevisIssue()
    {
        // Arrange
        var visDir = Path.Combine(_tempDirectory, "vis");
        Directory.CreateDirectory(visDir);
        await File.WriteAllBytesAsync(Path.Combine(visDir, "precombined.uvd"), new byte[] { 0, 0 });

        // Act
        var result = await _scanner.ScanAsync(_tempDirectory);

        // Assert
        result.HasIssues.Should().BeTrue();
        result.PrevisFileIssues.Should().HaveCount(1);
    }

    [Fact]
    public async Task ScanAsync_WithOcNifFile_ReportsPrevisIssue()
    {
        // Arrange
        var meshDir = Path.Combine(_tempDirectory, "meshes");
        Directory.CreateDirectory(meshDir);
        await File.WriteAllBytesAsync(Path.Combine(meshDir, "combined_oc.nif"), new byte[] { 0, 0 });

        // Act
        var result = await _scanner.ScanAsync(_tempDirectory);

        // Assert
        result.PrevisFileIssues.Should().HaveCount(1);
    }

    [Fact]
    public async Task ScanAsync_WithRegularNifFile_DoesNotReportPrevisIssue()
    {
        // Arrange
        var meshDir = Path.Combine(_tempDirectory, "meshes");
        Directory.CreateDirectory(meshDir);
        await File.WriteAllBytesAsync(Path.Combine(meshDir, "regular.nif"), new byte[] { 0, 0 });

        // Act
        var result = await _scanner.ScanAsync(_tempDirectory);

        // Assert
        result.PrevisFileIssues.Should().BeEmpty();
    }

    #endregion

    #region DDS Analysis Tests

    [Fact]
    public async Task ScanAsync_WithDdsFile_CallsDdsAnalyzer()
    {
        // Arrange
        var textureDir = Path.Combine(_tempDirectory, "textures");
        Directory.CreateDirectory(textureDir);
        var ddsPath = Path.Combine(textureDir, "texture.dds");
        await File.WriteAllBytesAsync(ddsPath, CreateValidDdsHeader());

        _mockDdsAnalyzer.Setup(x => x.AnalyzeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DDSInfo { Width = 512, Height = 512, IsCompressed = false });
        _mockDdsAnalyzer.Setup(x => x.IsValidBCDimensions(It.IsAny<int>(), It.IsAny<int>()))
            .Returns(true);

        // Act
        await _scanner.ScanAsync(_tempDirectory, analyzeDdsTextures: true);

        // Assert
        _mockDdsAnalyzer.Verify(x => x.AnalyzeAsync(
            It.Is<string>(s => s.EndsWith("texture.dds")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ScanAsync_WithDdsAnalysisDisabled_DoesNotCallDdsAnalyzer()
    {
        // Arrange
        var textureDir = Path.Combine(_tempDirectory, "textures");
        Directory.CreateDirectory(textureDir);
        await File.WriteAllBytesAsync(Path.Combine(textureDir, "texture.dds"), CreateValidDdsHeader());

        // Act
        await _scanner.ScanAsync(_tempDirectory, analyzeDdsTextures: false);

        // Assert
        _mockDdsAnalyzer.Verify(x => x.AnalyzeAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ScanAsync_WithOddDimensionDds_ReportsTextureDimensionIssue()
    {
        // Arrange
        var textureDir = Path.Combine(_tempDirectory, "textures");
        Directory.CreateDirectory(textureDir);
        var ddsPath = Path.Combine(textureDir, "texture.dds");
        await File.WriteAllBytesAsync(ddsPath, CreateValidDdsHeader());

        _mockDdsAnalyzer.Setup(x => x.AnalyzeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DDSInfo { Width = 513, Height = 512, IsCompressed = false });

        // Act
        var result = await _scanner.ScanAsync(_tempDirectory, analyzeDdsTextures: true);

        // Assert
        result.HasIssues.Should().BeTrue();
        result.TextureDimensionIssues.Should().HaveCount(1);
        result.TextureDimensionIssues[0].Width.Should().Be(513);
        result.TextureDimensionIssues[0].Issue.Should().Contain("Odd dimensions");
    }

    [Fact]
    public async Task ScanAsync_WithInvalidBCDimensions_ReportsTextureDimensionIssue()
    {
        // Arrange
        var textureDir = Path.Combine(_tempDirectory, "textures");
        Directory.CreateDirectory(textureDir);
        var ddsPath = Path.Combine(textureDir, "texture.dds");
        await File.WriteAllBytesAsync(ddsPath, CreateValidDdsHeader());

        _mockDdsAnalyzer.Setup(x => x.AnalyzeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DDSInfo { Width = 510, Height = 510, IsCompressed = true });
        _mockDdsAnalyzer.Setup(x => x.IsValidBCDimensions(510, 510)).Returns(false);

        // Act
        var result = await _scanner.ScanAsync(_tempDirectory, analyzeDdsTextures: true);

        // Assert
        result.TextureDimensionIssues.Should().HaveCount(1);
        result.TextureDimensionIssues[0].Issue.Should().Contain("BC compressed");
    }

    [Fact]
    public async Task ScanAsync_WithNoDdsAnalyzer_SkipsDdsAnalysis()
    {
        // Arrange
        var scannerWithoutAnalyzer = new UnpackedModsScanner();
        var textureDir = Path.Combine(_tempDirectory, "textures");
        Directory.CreateDirectory(textureDir);
        await File.WriteAllBytesAsync(Path.Combine(textureDir, "texture.dds"), CreateValidDdsHeader());

        // Act
        var result = await scannerWithoutAnalyzer.ScanAsync(_tempDirectory, analyzeDdsTextures: true);

        // Assert - should not throw and should not report dimension issues
        result.TextureDimensionIssues.Should().BeEmpty();
    }

    #endregion

    #region Progress Reporting Tests

    [Fact]
    public async Task ScanWithProgressAsync_ReportsProgress()
    {
        // Arrange
        var subDir = Path.Combine(_tempDirectory, "subdir");
        Directory.CreateDirectory(subDir);
        await File.WriteAllTextAsync(Path.Combine(_tempDirectory, "file1.txt"), "content");
        await File.WriteAllTextAsync(Path.Combine(subDir, "file2.txt"), "content");

        var progressReports = new List<UnpackedScanProgress>();
        var progress = new Progress<UnpackedScanProgress>(p => progressReports.Add(p));

        // Act
        await _scanner.ScanWithProgressAsync(_tempDirectory, null, false, progress);

        // Allow time for progress reports to be processed
        await Task.Delay(100);

        // Assert
        progressReports.Should().NotBeEmpty();
        progressReports.Last().DirectoriesScanned.Should().BeGreaterThan(0);
    }

    #endregion

    #region Complex Scenario Tests

    [Fact]
    public async Task ScanAsync_WithMultipleIssueTypes_ReportsAllIssues()
    {
        // Arrange
        // Create a complex mod structure with various issues
        await File.WriteAllTextAsync(Path.Combine(_tempDirectory, "readme.txt"), "content");

        var fomodDir = Path.Combine(_tempDirectory, "fomod");
        Directory.CreateDirectory(fomodDir);

        var textureDir = Path.Combine(_tempDirectory, "textures");
        Directory.CreateDirectory(textureDir);
        await File.WriteAllBytesAsync(Path.Combine(textureDir, "texture.tga"), new byte[] { 0 });

        var soundDir = Path.Combine(_tempDirectory, "sound");
        Directory.CreateDirectory(soundDir);
        await File.WriteAllBytesAsync(Path.Combine(soundDir, "sound.mp3"), new byte[] { 0 });

        var meshDir = Path.Combine(_tempDirectory, "meshes");
        Directory.CreateDirectory(meshDir);
        await File.WriteAllBytesAsync(Path.Combine(meshDir, "combined_oc.nif"), new byte[] { 0 });

        // Act
        var result = await _scanner.ScanAsync(_tempDirectory);

        // Assert
        result.HasIssues.Should().BeTrue();
        result.CleanupIssues.Should().HaveCountGreaterThanOrEqualTo(2); // readme + fomod
        result.TextureFormatIssues.Should().HaveCount(1);
        result.SoundFormatIssues.Should().HaveCount(1);
        result.PrevisFileIssues.Should().HaveCount(1);
    }

    [Fact]
    public async Task ScanAsync_WithDeepDirectoryStructure_ScansAllLevels()
    {
        // Arrange
        var deepDir = Path.Combine(_tempDirectory, "level1", "level2", "level3", "level4");
        Directory.CreateDirectory(deepDir);
        await File.WriteAllTextAsync(Path.Combine(deepDir, "readme.txt"), "content");

        // Act
        var result = await _scanner.ScanAsync(_tempDirectory);

        // Assert
        result.CleanupIssues.Should().HaveCount(1);
        result.CleanupIssues[0].RelativePath.Should().Contain("level1");
        result.CleanupIssues[0].RelativePath.Should().Contain("level4");
    }

    [Fact]
    public async Task ScanAsync_ReportsCorrectTotals()
    {
        // Arrange
        var subDir1 = Path.Combine(_tempDirectory, "dir1");
        var subDir2 = Path.Combine(_tempDirectory, "dir2");
        Directory.CreateDirectory(subDir1);
        Directory.CreateDirectory(subDir2);

        await File.WriteAllTextAsync(Path.Combine(_tempDirectory, "file1.txt"), "1");
        await File.WriteAllTextAsync(Path.Combine(subDir1, "file2.txt"), "2");
        await File.WriteAllTextAsync(Path.Combine(subDir1, "file3.txt"), "3");
        await File.WriteAllTextAsync(Path.Combine(subDir2, "file4.txt"), "4");

        // Act
        var result = await _scanner.ScanAsync(_tempDirectory);

        // Assert
        result.TotalDirectoriesScanned.Should().Be(3); // root + 2 subdirs
        result.TotalFilesScanned.Should().Be(4);
    }

    #endregion

    #region Model Tests

    [Fact]
    public void UnpackedScanResult_HasIssues_ReturnsTrueWhenAnyIssuePresent()
    {
        // Arrange & Act
        var resultWithCleanup = new UnpackedScanResult
        {
            CleanupIssues = new[] { new CleanupIssue("path", "rel", CleanupItemType.ReadmeFile) }
        };

        var resultWithTexture = new UnpackedScanResult
        {
            TextureFormatIssues = new[] { new UnpackedTextureFormatIssue("path", "rel", "TGA") }
        };

        var resultWithPrevis = new UnpackedScanResult
        {
            PrevisFileIssues = new[] { new PrevisFileIssue("path", "rel") }
        };

        var resultWithNoIssues = new UnpackedScanResult();

        // Assert
        resultWithCleanup.HasIssues.Should().BeTrue();
        resultWithTexture.HasIssues.Should().BeTrue();
        resultWithPrevis.HasIssues.Should().BeTrue();
        resultWithNoIssues.HasIssues.Should().BeFalse();
    }

    [Fact]
    public void CleanupIssue_RecordEquality_WorksCorrectly()
    {
        // Arrange
        var issue1 = new CleanupIssue("path", "rel", CleanupItemType.ReadmeFile);
        var issue2 = new CleanupIssue("path", "rel", CleanupItemType.ReadmeFile);
        var issue3 = new CleanupIssue("path", "rel", CleanupItemType.FomodFolder);

        // Assert
        issue1.Should().Be(issue2);
        issue1.Should().NotBe(issue3);
    }

    [Fact]
    public void UnpackedScanProgress_ContainsCorrectData()
    {
        // Arrange & Act
        var progress = new UnpackedScanProgress("current/dir", 10, 50, 5);

        // Assert
        progress.CurrentDirectory.Should().Be("current/dir");
        progress.DirectoriesScanned.Should().Be(10);
        progress.FilesScanned.Should().Be(50);
        progress.IssuesFound.Should().Be(5);
    }

    #endregion

    #region Helper Methods

    private static byte[] CreateValidDdsHeader()
    {
        // Minimal DDS header (128 bytes)
        var header = new byte[128];
        // Magic "DDS "
        header[0] = 0x44; // D
        header[1] = 0x44; // D
        header[2] = 0x53; // S
        header[3] = 0x20; // (space)
        // Header size (124)
        header[4] = 0x7C;
        return header;
    }

    #endregion
}
