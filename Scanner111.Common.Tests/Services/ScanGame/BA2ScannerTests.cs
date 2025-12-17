using System.Text;
using FluentAssertions;
using Scanner111.Common.Models.ScanGame;
using Scanner111.Common.Services.ScanGame;

namespace Scanner111.Common.Tests.Services.ScanGame;

/// <summary>
/// Tests for BA2Scanner and related components.
/// </summary>
public class BA2ScannerTests : IDisposable
{
    private readonly BA2Scanner _scanner;
    private readonly string _tempDirectory;

    public BA2ScannerTests()
    {
        _scanner = new BA2Scanner();
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"BA2ScannerTests_{Guid.NewGuid():N}");
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

    #region Header Parsing Tests

    [Fact]
    public async Task ReadHeaderAsync_WithValidGeneralHeader_ReturnsValidInfo()
    {
        // Arrange
        var ba2File = CreateValidBA2File("test.ba2", BA2Format.General);

        // Act
        var headerInfo = await _scanner.ReadHeaderAsync(ba2File);

        // Assert
        headerInfo.IsValid.Should().BeTrue();
        headerInfo.Format.Should().Be(BA2Format.General);
        headerInfo.Version.Should().BeGreaterThan(0u);
    }

    [Fact]
    public async Task ReadHeaderAsync_WithValidTextureHeader_ReturnsValidInfo()
    {
        // Arrange
        var ba2File = CreateValidBA2File("textures.ba2", BA2Format.Texture);

        // Act
        var headerInfo = await _scanner.ReadHeaderAsync(ba2File);

        // Assert
        headerInfo.IsValid.Should().BeTrue();
        headerInfo.Format.Should().Be(BA2Format.Texture);
    }

    [Fact]
    public async Task ReadHeaderAsync_WithInvalidMagic_ReturnsInvalid()
    {
        // Arrange
        var ba2File = Path.Combine(_tempDirectory, "invalid_magic.ba2");
        var header = new byte[12];
        Encoding.ASCII.GetBytes("XXXX").CopyTo(header, 0);  // Wrong magic
        BitConverter.GetBytes(1u).CopyTo(header, 4);        // Version
        Encoding.ASCII.GetBytes("GNRL").CopyTo(header, 8);  // Format
        await File.WriteAllBytesAsync(ba2File, header);

        // Act
        var headerInfo = await _scanner.ReadHeaderAsync(ba2File);

        // Assert
        headerInfo.IsValid.Should().BeFalse();
        headerInfo.Format.Should().Be(BA2Format.Unknown);
    }

    [Fact]
    public async Task ReadHeaderAsync_WithInvalidFormat_ReturnsInvalid()
    {
        // Arrange
        var ba2File = Path.Combine(_tempDirectory, "invalid_format.ba2");
        var header = new byte[12];
        Encoding.ASCII.GetBytes("BTDX").CopyTo(header, 0);  // Correct magic
        BitConverter.GetBytes(1u).CopyTo(header, 4);        // Version
        Encoding.ASCII.GetBytes("XXXX").CopyTo(header, 8);  // Wrong format
        await File.WriteAllBytesAsync(ba2File, header);

        // Act
        var headerInfo = await _scanner.ReadHeaderAsync(ba2File);

        // Assert
        headerInfo.IsValid.Should().BeFalse();
        headerInfo.Format.Should().Be(BA2Format.Unknown);
    }

    [Fact]
    public async Task ReadHeaderAsync_WithTruncatedFile_ReturnsInvalid()
    {
        // Arrange
        var ba2File = Path.Combine(_tempDirectory, "truncated.ba2");
        await File.WriteAllBytesAsync(ba2File, new byte[4]); // Only 4 bytes

        // Act
        var headerInfo = await _scanner.ReadHeaderAsync(ba2File);

        // Assert
        headerInfo.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ReadHeaderAsync_WithEmptyFile_ReturnsInvalid()
    {
        // Arrange
        var ba2File = Path.Combine(_tempDirectory, "empty.ba2");
        await File.WriteAllBytesAsync(ba2File, Array.Empty<byte>());

        // Act
        var headerInfo = await _scanner.ReadHeaderAsync(ba2File);

        // Assert
        headerInfo.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ReadHeaderAsync_WithNonExistentFile_ReturnsInvalid()
    {
        // Act
        var headerInfo = await _scanner.ReadHeaderAsync(Path.Combine(_tempDirectory, "nonexistent.ba2"));

        // Assert
        headerInfo.IsValid.Should().BeFalse();
    }

    #endregion

    #region File Discovery Tests

    [Fact]
    public async Task FindBA2FilesAsync_WithBA2Files_FindsAll()
    {
        // Arrange
        CreateValidBA2File("mod1.ba2", BA2Format.General);
        CreateValidBA2File("mod2.ba2", BA2Format.Texture);

        // Act
        var files = await _scanner.FindBA2FilesAsync(_tempDirectory);

        // Assert
        files.Should().HaveCount(2);
        files.Should().Contain(f => f.EndsWith("mod1.ba2"));
        files.Should().Contain(f => f.EndsWith("mod2.ba2"));
    }

    [Fact]
    public async Task FindBA2FilesAsync_WithSubdirectories_FindsRecursively()
    {
        // Arrange
        var subDir = Path.Combine(_tempDirectory, "subdir");
        Directory.CreateDirectory(subDir);

        CreateValidBA2File("root.ba2", BA2Format.General);
        CreateValidBA2File("subdir/nested.ba2", BA2Format.General);

        // Act
        var files = await _scanner.FindBA2FilesAsync(_tempDirectory);

        // Assert
        files.Should().HaveCount(2);
        files.Should().Contain(f => f.EndsWith("root.ba2"));
        files.Should().Contain(f => f.EndsWith("nested.ba2"));
    }

    [Fact]
    public async Task FindBA2FilesAsync_ExcludesPrpMainBA2()
    {
        // Arrange
        CreateValidBA2File("mod.ba2", BA2Format.General);
        CreateValidBA2File("prp - main.ba2", BA2Format.General);

        // Act
        var files = await _scanner.FindBA2FilesAsync(_tempDirectory);

        // Assert
        files.Should().HaveCount(1);
        files.Should().NotContain(f => f.Contains("prp - main.ba2"));
    }

    [Fact]
    public async Task FindBA2FilesAsync_WithNoBA2Files_ReturnsEmpty()
    {
        // Arrange - create non-BA2 files
        await File.WriteAllTextAsync(Path.Combine(_tempDirectory, "readme.txt"), "test");

        // Act
        var files = await _scanner.FindBA2FilesAsync(_tempDirectory);

        // Assert
        files.Should().BeEmpty();
    }

    [Fact]
    public async Task FindBA2FilesAsync_WithNonExistentDirectory_ReturnsEmpty()
    {
        // Act
        var files = await _scanner.FindBA2FilesAsync(Path.Combine(_tempDirectory, "nonexistent"));

        // Assert
        files.Should().BeEmpty();
    }

    [Fact]
    public async Task FindBA2FilesAsync_IsCaseInsensitive()
    {
        // Arrange
        var ba2File = Path.Combine(_tempDirectory, "UPPERCASE.BA2");
        var header = CreateBA2Header(BA2Format.General);
        await File.WriteAllBytesAsync(ba2File, header);

        // Act
        var files = await _scanner.FindBA2FilesAsync(_tempDirectory);

        // Assert
        files.Should().HaveCount(1);
    }

    #endregion

    #region Full Scan Tests

    [Fact]
    public async Task ScanAsync_WithValidFiles_ReturnsCorrectCount()
    {
        // Arrange
        CreateValidBA2File("valid1.ba2", BA2Format.General);
        CreateValidBA2File("valid2.ba2", BA2Format.Texture);

        // Act
        var result = await _scanner.ScanAsync(_tempDirectory);

        // Assert
        result.TotalFilesScanned.Should().Be(2);
        result.HasIssues.Should().BeFalse();
        result.FormatIssues.Should().BeEmpty();
    }

    [Fact]
    public async Task ScanAsync_WithInvalidHeader_ReportsFormatIssue()
    {
        // Arrange
        CreateValidBA2File("valid.ba2", BA2Format.General);

        var invalidFile = Path.Combine(_tempDirectory, "invalid.ba2");
        var invalidHeader = new byte[12];
        Encoding.ASCII.GetBytes("XXXX").CopyTo(invalidHeader, 0);
        await File.WriteAllBytesAsync(invalidFile, invalidHeader);

        // Act
        var result = await _scanner.ScanAsync(_tempDirectory);

        // Assert
        result.TotalFilesScanned.Should().Be(2);
        result.HasIssues.Should().BeTrue();
        result.FormatIssues.Should().HaveCount(1);
        result.FormatIssues[0].ArchiveName.Should().Be("invalid.ba2");
    }

    [Fact]
    public async Task ScanAsync_WithEmptyDirectory_ReturnsZeroFiles()
    {
        // Act
        var result = await _scanner.ScanAsync(_tempDirectory);

        // Assert
        result.TotalFilesScanned.Should().Be(0);
        result.HasIssues.Should().BeFalse();
    }

    [Fact]
    public async Task ScanAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        CreateValidBA2File("test.ba2", BA2Format.General);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _scanner.ScanAsync(_tempDirectory, cancellationToken: cts.Token));
    }

    [Fact]
    public async Task ScanAsync_WithNullOrEmptyPath_ThrowsArgumentException()
    {
        // Act & Assert
        // ArgumentException.ThrowIfNullOrWhiteSpace throws ArgumentNullException for null,
        // and ArgumentException for empty/whitespace
        await Assert.ThrowsAsync<ArgumentNullException>(() => _scanner.ScanAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => _scanner.ScanAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => _scanner.ScanAsync("   "));
    }

    [Fact]
    public async Task ScanAsync_WithMixedValidAndInvalidFiles_ReportsAllIssues()
    {
        // Arrange
        CreateValidBA2File("valid1.ba2", BA2Format.General);
        CreateValidBA2File("valid2.ba2", BA2Format.Texture);

        // Create two invalid files
        var invalid1 = Path.Combine(_tempDirectory, "invalid1.ba2");
        var invalid2 = Path.Combine(_tempDirectory, "invalid2.ba2");
        await File.WriteAllBytesAsync(invalid1, Encoding.ASCII.GetBytes("XXXX12345678"));
        await File.WriteAllBytesAsync(invalid2, Encoding.ASCII.GetBytes("BADHEADERDATA"));

        // Act
        var result = await _scanner.ScanAsync(_tempDirectory);

        // Assert
        result.TotalFilesScanned.Should().Be(4);
        result.HasIssues.Should().BeTrue();
        result.FormatIssues.Should().HaveCount(2);
    }

    #endregion

    #region Model Tests

    [Fact]
    public void BA2ScanResult_HasIssues_ReturnsTrueWhenAnyIssuePresent()
    {
        // Arrange & Act
        var resultWithFormatIssue = new BA2ScanResult
        {
            FormatIssues = new[] { new BA2FormatIssue("path", "name", "header") }
        };

        var resultWithTextureIssue = new BA2ScanResult
        {
            TextureDimensionIssues = new[] { new TextureDimensionIssue("archive", "texture", 513, 512) }
        };

        var resultWithNoIssues = new BA2ScanResult();

        // Assert
        resultWithFormatIssue.HasIssues.Should().BeTrue();
        resultWithTextureIssue.HasIssues.Should().BeTrue();
        resultWithNoIssues.HasIssues.Should().BeFalse();
    }

    [Fact]
    public void BA2HeaderInfo_RecordEquality_WorksCorrectly()
    {
        // Arrange
        var header1 = new BA2HeaderInfo(true, BA2Format.General, 1);
        var header2 = new BA2HeaderInfo(true, BA2Format.General, 1);
        var header3 = new BA2HeaderInfo(true, BA2Format.Texture, 1);

        // Assert
        header1.Should().Be(header2);
        header1.Should().NotBe(header3);
    }

    #endregion

    #region Helper Methods

    private string CreateValidBA2File(string relativePath, BA2Format format)
    {
        var fullPath = Path.Combine(_tempDirectory, relativePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var header = CreateBA2Header(format);
        File.WriteAllBytes(fullPath, header);
        return fullPath;
    }

    private static byte[] CreateBA2Header(BA2Format format)
    {
        var header = new byte[12];
        Encoding.ASCII.GetBytes("BTDX").CopyTo(header, 0);  // Magic
        BitConverter.GetBytes(1u).CopyTo(header, 4);        // Version

        var formatBytes = format switch
        {
            BA2Format.General => "GNRL",
            BA2Format.Texture => "DX10",
            _ => "XXXX"
        };
        Encoding.ASCII.GetBytes(formatBytes).CopyTo(header, 8);

        return header;
    }

    #endregion
}
