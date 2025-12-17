using System.Text;
using FluentAssertions;
using Scanner111.Common.Models.ScanGame;
using Scanner111.Common.Services.ScanGame;

namespace Scanner111.Common.Tests.Services.ScanGame;

/// <summary>
/// Tests for DDSAnalyzer and related components.
/// </summary>
public class DDSAnalyzerTests : IDisposable
{
    private readonly DDSAnalyzer _analyzer;
    private readonly string _tempDirectory;

    public DDSAnalyzerTests()
    {
        _analyzer = new DDSAnalyzer();
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"DDSAnalyzerTests_{Guid.NewGuid():N}");
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
    public void AnalyzeFromBytes_WithValidDXT1Header_ReturnsCorrectInfo()
    {
        // Arrange
        var header = CreateDDSHeader(1024, 512, "DXT1", mipmaps: 10);

        // Act
        var info = _analyzer.AnalyzeFromBytes(header, header.Length);

        // Assert
        info.Should().NotBeNull();
        info!.Width.Should().Be(1024);
        info.Height.Should().Be(512);
        info.FormatFourCC.Should().Be("DXT1");
        info.IsCompressed.Should().BeTrue();
        info.MipmapCount.Should().Be(10);
        info.PixelFormat.Should().Contain("BC1");
    }

    [Fact]
    public void AnalyzeFromBytes_WithValidDXT5Header_ReturnsCorrectInfo()
    {
        // Arrange
        var header = CreateDDSHeader(2048, 2048, "DXT5", mipmaps: 12);

        // Act
        var info = _analyzer.AnalyzeFromBytes(header, header.Length);

        // Assert
        info.Should().NotBeNull();
        info!.Width.Should().Be(2048);
        info.Height.Should().Be(2048);
        info.FormatFourCC.Should().Be("DXT5");
        info.IsCompressed.Should().BeTrue();
    }

    [Fact]
    public void AnalyzeFromBytes_WithDX10Header_DetectsDX10Format()
    {
        // Arrange
        var header = CreateDDSHeader(1024, 1024, "DX10", mipmaps: 1);

        // Act
        var info = _analyzer.AnalyzeFromBytes(header, header.Length);

        // Assert
        info.Should().NotBeNull();
        info!.FormatFourCC.Should().Be("DX10");
        info.IsDx10.Should().BeTrue();
    }

    [Fact]
    public void AnalyzeFromBytes_WithInvalidMagic_ReturnsNull()
    {
        // Arrange
        var header = CreateDDSHeader(1024, 1024, "DXT1");
        // Corrupt the magic
        header[0] = (byte)'X';

        // Act
        var info = _analyzer.AnalyzeFromBytes(header, header.Length);

        // Assert
        info.Should().BeNull();
    }

    [Fact]
    public void AnalyzeFromBytes_WithTruncatedData_ReturnsNull()
    {
        // Arrange
        var header = new byte[64]; // Too short

        // Act
        var info = _analyzer.AnalyzeFromBytes(header, header.Length);

        // Assert
        info.Should().BeNull();
    }

    [Fact]
    public void AnalyzeFromBytes_WithInvalidHeaderSize_ReturnsNull()
    {
        // Arrange
        var header = CreateDDSHeader(1024, 1024, "DXT1");
        // Corrupt the header size field (should be 124)
        BitConverter.GetBytes(100).CopyTo(header, 4);

        // Act
        var info = _analyzer.AnalyzeFromBytes(header, header.Length);

        // Assert
        info.Should().BeNull();
    }

    #endregion

    #region File Analysis Tests

    [Fact]
    public async Task AnalyzeAsync_WithValidDDSFile_ReturnsInfo()
    {
        // Arrange
        var ddsFile = CreateDDSFile("test.dds", 512, 512, "DXT5");

        // Act
        var info = await _analyzer.AnalyzeAsync(ddsFile);

        // Assert
        info.Should().NotBeNull();
        info!.Width.Should().Be(512);
        info.Height.Should().Be(512);
        info.FilePath.Should().Be(ddsFile);
    }

    [Fact]
    public async Task AnalyzeAsync_WithNonExistentFile_ReturnsNull()
    {
        // Act
        var info = await _analyzer.AnalyzeAsync(Path.Combine(_tempDirectory, "nonexistent.dds"));

        // Assert
        info.Should().BeNull();
    }

    [Fact]
    public async Task AnalyzeAsync_WithTooSmallFile_ReturnsNull()
    {
        // Arrange
        var smallFile = Path.Combine(_tempDirectory, "small.dds");
        await File.WriteAllBytesAsync(smallFile, new byte[64]);

        // Act
        var info = await _analyzer.AnalyzeAsync(smallFile);

        // Assert
        info.Should().BeNull();
    }

    [Fact]
    public async Task AnalyzeAsync_WithNullPath_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _analyzer.AnalyzeAsync(null!));
    }

    #endregion

    #region Validation Tests

    [Fact]
    public void ValidateForGame_WithValidTexture_ReturnsNoIssues()
    {
        // Arrange
        var info = new DDSInfo
        {
            Width = 1024,
            Height = 1024,
            IsCompressed = true,
            MipmapCount = 10,
            FormatFourCC = "DXT5"
        };

        // Act
        var result = _analyzer.ValidateForGame(info);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void ValidateForGame_WithOddDimensions_ReportsIssue()
    {
        // Arrange
        var info = new DDSInfo
        {
            Width = 1023,
            Height = 512,
            IsCompressed = false,
            MipmapCount = 1
        };

        // Act
        var result = _analyzer.ValidateForGame(info);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Contains("Odd dimensions"));
    }

    [Fact]
    public void ValidateForGame_WithInvalidBCDimensions_ReportsIssue()
    {
        // Arrange
        var info = new DDSInfo
        {
            Width = 1002, // Not multiple of 4 (1002 % 4 = 2)
            Height = 1002,
            IsCompressed = true,
            MipmapCount = 1,
            FormatFourCC = "DXT5"
        };

        // Act
        var result = _analyzer.ValidateForGame(info);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Contains("multiple of 4"));
    }

    [Fact]
    public void ValidateForGame_WithHugeDimensions_ReportsIssue()
    {
        // Arrange
        var info = new DDSInfo
        {
            Width = 16384,
            Height = 8192,
            IsCompressed = true,
            MipmapCount = 1
        };

        // Act
        var result = _analyzer.ValidateForGame(info);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Contains("8192"));
    }

    [Fact]
    public void ValidateForGame_WithNoMipmapsOnLargeTexture_ReportsWarning()
    {
        // Arrange
        var info = new DDSInfo
        {
            Width = 1024,
            Height = 1024,
            IsCompressed = true,
            MipmapCount = 1,
            FormatFourCC = "DXT5"
        };

        // Act
        var result = _analyzer.ValidateForGame(info);

        // Assert
        result.Warnings.Should().Contain(w => w.Contains("mipmaps"));
    }

    [Fact]
    public void ValidateForGame_Fallout4_WithLargeTexture_ReportsWarning()
    {
        // Arrange
        var info = new DDSInfo
        {
            Width = 8192,
            Height = 8192,
            IsCompressed = true,
            MipmapCount = 13,
            FormatFourCC = "DXT5"
        };

        // Act
        var result = _analyzer.ValidateForGame(info, "Fallout4");

        // Assert
        result.Warnings.Should().Contain(w => w.Contains("4096"));
    }

    #endregion

    #region DDSInfo Property Tests

    [Fact]
    public void DDSInfo_IsPowerOf2_ReturnsTrueForPowerOf2()
    {
        // Arrange
        var info = new DDSInfo { Width = 1024, Height = 512 };

        // Assert
        info.IsPowerOf2.Should().BeTrue();
    }

    [Fact]
    public void DDSInfo_IsPowerOf2_ReturnsFalseForNonPowerOf2()
    {
        // Arrange
        var info = new DDSInfo { Width = 1000, Height = 512 };

        // Assert
        info.IsPowerOf2.Should().BeFalse();
    }

    [Fact]
    public void DDSInfo_IsBCCompatible_ReturnsTrueForMultiplesOf4()
    {
        // Arrange
        var info = new DDSInfo { Width = 1024, Height = 512 };

        // Assert
        info.IsBCCompatible.Should().BeTrue();
    }

    [Fact]
    public void DDSInfo_IsBCCompatible_ReturnsFalseForNonMultiplesOf4()
    {
        // Arrange
        var info = new DDSInfo { Width = 1022, Height = 512 };

        // Assert
        info.IsBCCompatible.Should().BeFalse();
    }

    [Fact]
    public void DDSInfo_AspectRatio_CalculatesCorrectly()
    {
        // Arrange
        var info = new DDSInfo { Width = 1920, Height = 1080 };

        // Assert
        info.AspectRatio.Should().BeApproximately(1.777f, 0.01f);
    }

    [Fact]
    public void DDSInfo_TotalPixels_CalculatesWithMipmaps()
    {
        // Arrange
        var info = new DDSInfo { Width = 1024, Height = 1024, MipmapCount = 2, Depth = 1 };

        // Assert
        // Mip 0: 1024*1024 = 1048576
        // Mip 1: 512*512 = 262144
        // Total: 1310720
        info.TotalPixels.Should().Be(1310720);
    }

    #endregion

    #region IsValidBCDimensions Tests

    [Theory]
    [InlineData(1024, 1024, true)]
    [InlineData(512, 256, true)]
    [InlineData(4, 4, true)]
    [InlineData(1023, 1024, false)]
    [InlineData(1024, 1023, false)]
    [InlineData(3, 4, false)]
    public void IsValidBCDimensions_ReturnsCorrectResult(int width, int height, bool expected)
    {
        // Act
        var result = _analyzer.IsValidBCDimensions(width, height);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region Helper Methods

    private byte[] CreateDDSHeader(int width, int height, string fourCC, int mipmaps = 1, bool hasAlpha = false)
    {
        // DDS file format:
        // - 4 bytes: "DDS " magic
        // - 124 bytes: DDS_HEADER
        var header = new byte[128];

        // Magic "DDS "
        Encoding.ASCII.GetBytes("DDS ").CopyTo(header, 0);

        // DDS_HEADER
        // dwSize (124)
        BitConverter.GetBytes(124).CopyTo(header, 4);

        // dwFlags (CAPS | HEIGHT | WIDTH | PIXELFORMAT | MIPMAPCOUNT)
        uint flags = 0x1 | 0x2 | 0x4 | 0x1000;
        if (mipmaps > 1) flags |= 0x20000;
        BitConverter.GetBytes(flags).CopyTo(header, 8);

        // dwHeight
        BitConverter.GetBytes(height).CopyTo(header, 12);

        // dwWidth
        BitConverter.GetBytes(width).CopyTo(header, 16);

        // dwPitchOrLinearSize (offset 20) - skip
        // dwDepth (offset 24) - skip

        // dwMipMapCount (offset 28)
        BitConverter.GetBytes(mipmaps).CopyTo(header, 28);

        // dwReserved1[11] (offsets 32-75) - skip

        // DDS_PIXELFORMAT at offset 76 (header[72] since we account for magic at [0-3])
        // But our header array starts at 0 with magic, so pixel format is at offset 76
        var pfOffset = 76;

        // dwSize (32)
        BitConverter.GetBytes(32).CopyTo(header, pfOffset);

        // dwFlags (FOURCC = 0x4, ALPHAPIXELS = 0x1)
        uint pfFlags = 0x4; // FOURCC
        if (hasAlpha) pfFlags |= 0x1;
        BitConverter.GetBytes(pfFlags).CopyTo(header, pfOffset + 4);

        // dwFourCC
        Encoding.ASCII.GetBytes(fourCC.PadRight(4, '\0')[..4]).CopyTo(header, pfOffset + 8);

        // dwRGBBitCount (offset pfOffset + 12) - 0 for compressed
        // dwRBitMask, dwGBitMask, dwBBitMask, dwABitMask - skip for compressed

        // dwCaps (offset 108)
        BitConverter.GetBytes(0x1000u).CopyTo(header, 108); // DDSCAPS_TEXTURE

        return header;
    }

    private string CreateDDSFile(string relativePath, int width, int height, string fourCC)
    {
        var fullPath = Path.Combine(_tempDirectory, relativePath);
        var header = CreateDDSHeader(width, height, fourCC);
        File.WriteAllBytes(fullPath, header);
        return fullPath;
    }

    #endregion
}
