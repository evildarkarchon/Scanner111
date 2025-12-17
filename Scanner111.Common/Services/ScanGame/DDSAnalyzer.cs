using System.Buffers.Binary;
using System.Text;
using Scanner111.Common.Models.ScanGame;

namespace Scanner111.Common.Services.ScanGame;

/// <summary>
/// Provides functionality for analyzing and validating DDS (DirectDraw Surface) texture files.
/// </summary>
/// <remarks>
/// DDS file format:
/// - Magic: "DDS " (4 bytes)
/// - Header: 124 bytes (DDS_HEADER structure)
/// - Optional DX10 extended header: 20 bytes (DDS_HEADER_DXT10)
/// - Pixel data
/// </remarks>
public sealed class DDSAnalyzer : IDDSAnalyzer
{
    private const int MinHeaderSize = 128; // Magic (4) + Header (124)
    private const int Dx10ExtendedHeaderSize = 20;
    private static readonly byte[] DdsMagic = Encoding.ASCII.GetBytes("DDS ");

    /// <summary>
    /// Known BC/DXT format FourCC codes and their descriptions.
    /// </summary>
    private static readonly Dictionary<string, string> BCFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DXT1"] = "BC1/DXT1 (4bpp, 1-bit alpha)",
        ["DXT2"] = "BC2/DXT2 (8bpp, premult alpha)",
        ["DXT3"] = "BC2/DXT3 (8bpp, explicit alpha)",
        ["DXT4"] = "BC3/DXT4 (8bpp, premult alpha)",
        ["DXT5"] = "BC3/DXT5 (8bpp, interpolated alpha)",
        ["BC4U"] = "BC4 Unsigned (4bpp, single channel)",
        ["BC4S"] = "BC4 Signed (4bpp, single channel)",
        ["BC5U"] = "BC5 Unsigned (8bpp, two channels)",
        ["BC5S"] = "BC5 Signed (8bpp, two channels)",
        ["ATI1"] = "BC4/ATI1 (4bpp, single channel)",
        ["ATI2"] = "BC5/ATI2 (8bpp, two channels)",
        ["DX10"] = "DX10 Extended Header"
    };

    /// <inheritdoc />
    public async Task<DDSInfo?> AnalyzeAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        try
        {
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists || fileInfo.Length < MinHeaderSize)
            {
                return null;
            }

            // Read enough bytes for header + potential DX10 extension
            var headerBytes = new byte[MinHeaderSize + Dx10ExtendedHeaderSize];
            await using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: headerBytes.Length,
                useAsync: true);

            var bytesRead = await stream.ReadAsync(headerBytes.AsMemory(), cancellationToken)
                .ConfigureAwait(false);

            if (bytesRead < MinHeaderSize)
            {
                return null;
            }

            var info = AnalyzeFromBytes(headerBytes.AsSpan(0, bytesRead), fileInfo.Length);
            if (info is not null)
            {
                return info with { FilePath = filePath };
            }

            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public DDSInfo? AnalyzeFromBytes(ReadOnlySpan<byte> headerBytes, long fileSize = 0)
    {
        if (headerBytes.Length < MinHeaderSize)
        {
            return null;
        }

        // Check magic number
        if (!headerBytes[..4].SequenceEqual(DdsMagic))
        {
            return null;
        }

        // Parse DDS header (starts at byte 4)
        var header = headerBytes.Slice(4, 124);

        // Validate header size field (should be 124)
        var dwSize = BinaryPrimitives.ReadUInt32LittleEndian(header);
        if (dwSize != 124)
        {
            return null;
        }

        // Parse header fields
        var dwFlags = (DDSFlags)BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(4));
        var dwHeight = (int)BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(8));
        var dwWidth = (int)BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(12));
        var dwDepth = dwFlags.HasFlag(DDSFlags.Depth)
            ? (int)BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(20))
            : 1;
        var dwMipMapCount = dwFlags.HasFlag(DDSFlags.MipmapCount)
            ? Math.Max(1, (int)BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(24)))
            : 1;

        // Parse pixel format (32 bytes at offset 72 in header, 76 from start)
        var pixelFormat = header.Slice(72, 32);
        var pfSize = BinaryPrimitives.ReadUInt32LittleEndian(pixelFormat);
        if (pfSize != 32)
        {
            return null;
        }

        var pfFlags = (DDSPixelFlags)BinaryPrimitives.ReadUInt32LittleEndian(pixelFormat.Slice(4));
        string? fourcc = null;
        if (pfFlags.HasFlag(DDSPixelFlags.FourCC))
        {
            fourcc = Encoding.ASCII.GetString(pixelFormat.Slice(8, 4)).TrimEnd('\0');
        }

        // Parse caps for cubemap/volume detection
        var caps2 = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(108));
        var isCubemap = (caps2 & 0x200) != 0;   // DDSCAPS2_CUBEMAP
        var isVolume = (caps2 & 0x200000) != 0; // DDSCAPS2_VOLUME

        // Determine format details
        var (pixelFormatName, isCompressed) = DeterminePixelFormat(pfFlags, fourcc, pixelFormat);
        var hasAlpha = pfFlags.HasFlag(DDSPixelFlags.AlphaPixels);
        var isDx10 = fourcc == "DX10";

        return new DDSInfo
        {
            Width = dwWidth,
            Height = dwHeight,
            Depth = dwDepth,
            MipmapCount = dwMipMapCount,
            FormatFourCC = fourcc,
            IsCompressed = isCompressed,
            HasAlpha = hasAlpha,
            IsDx10 = isDx10,
            IsCubemap = isCubemap,
            IsVolume = isVolume,
            PixelFormat = pixelFormatName,
            FileSize = fileSize
        };
    }

    /// <inheritdoc />
    public DDSValidationResult ValidateForGame(DDSInfo info, string game = "Fallout4")
    {
        var issues = new List<string>();
        var warnings = new List<string>();

        // Common validations
        if (!info.IsPowerOf2 && info.MipmapCount > 1)
        {
            warnings.Add($"Non-power-of-2 dimensions ({info.Width}x{info.Height}) with mipmaps may cause issues");
        }

        if (info.IsCompressed && !info.IsBCCompatible)
        {
            issues.Add($"BC compressed format requires dimensions multiple of 4 (got {info.Width}x{info.Height})");
        }

        if (info.Width > 8192 || info.Height > 8192)
        {
            issues.Add($"Exceeds recommended maximum dimensions of 8192x8192 (got {info.Width}x{info.Height})");
        }

        if (info.Width % 2 != 0 || info.Height % 2 != 0)
        {
            issues.Add($"Odd dimensions ({info.Width}x{info.Height}) may cause rendering issues");
        }

        if (info.MipmapCount <= 1 && (info.Width > 256 || info.Height > 256))
        {
            warnings.Add("Large texture without mipmaps may cause performance issues");
        }

        // Game-specific validations
        if (game.Equals("Fallout4", StringComparison.OrdinalIgnoreCase))
        {
            if (info.Width > 4096 || info.Height > 4096)
            {
                warnings.Add($"Fallout 4 performs better with textures ≤4096x4096 (got {info.Width}x{info.Height})");
            }

            if (info.FormatFourCC == "DXT1" && info.HasAlpha)
            {
                warnings.Add("DXT1 with alpha may cause transparency issues in Fallout 4");
            }

            if (!info.IsCompressed && info.Width * info.Height > 1024 * 1024)
            {
                warnings.Add("Large uncompressed texture may cause performance issues");
            }
        }

        return new DDSValidationResult
        {
            Issues = issues.AsReadOnly(),
            Warnings = warnings.AsReadOnly()
        };
    }

    /// <inheritdoc />
    public bool IsValidBCDimensions(int width, int height)
    {
        return width % 4 == 0 && height % 4 == 0;
    }

    /// <summary>
    /// Determines the pixel format name and compression status from the pixel format data.
    /// </summary>
    private static (string FormatName, bool IsCompressed) DeterminePixelFormat(
        DDSPixelFlags flags,
        string? fourcc,
        ReadOnlySpan<byte> pixelFormat)
    {
        if (flags.HasFlag(DDSPixelFlags.FourCC) && fourcc is not null)
        {
            var formatName = BCFormats.TryGetValue(fourcc, out var known)
                ? known
                : $"FourCC: {fourcc}";
            return (formatName, true);
        }

        if (flags.HasFlag(DDSPixelFlags.RGB))
        {
            var rgbBitCount = BinaryPrimitives.ReadUInt32LittleEndian(pixelFormat.Slice(12));
            var formatName = flags.HasFlag(DDSPixelFlags.AlphaPixels)
                ? $"RGBA{rgbBitCount}"
                : $"RGB{rgbBitCount}";
            return (formatName, false);
        }

        if (flags.HasFlag(DDSPixelFlags.Luminance))
        {
            return ("Luminance", false);
        }

        if (flags.HasFlag(DDSPixelFlags.Alpha))
        {
            return ("Alpha", false);
        }

        return ("Unknown", false);
    }
}
