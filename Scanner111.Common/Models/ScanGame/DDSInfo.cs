namespace Scanner111.Common.Models.ScanGame;

/// <summary>
/// Comprehensive DDS (DirectDraw Surface) file information.
/// </summary>
public record DDSInfo
{
    /// <summary>
    /// Gets the texture width in pixels.
    /// </summary>
    public int Width { get; init; }

    /// <summary>
    /// Gets the texture height in pixels.
    /// </summary>
    public int Height { get; init; }

    /// <summary>
    /// Gets the texture depth (for volume textures). Default is 1.
    /// </summary>
    public int Depth { get; init; } = 1;

    /// <summary>
    /// Gets the number of mipmap levels. Default is 1.
    /// </summary>
    public int MipmapCount { get; init; } = 1;

    /// <summary>
    /// Gets the FourCC format code (e.g., "DXT1", "DXT5", "DX10").
    /// </summary>
    public string? FormatFourCC { get; init; }

    /// <summary>
    /// Gets a value indicating whether the texture uses block compression.
    /// </summary>
    public bool IsCompressed { get; init; }

    /// <summary>
    /// Gets a value indicating whether the texture has an alpha channel.
    /// </summary>
    public bool HasAlpha { get; init; }

    /// <summary>
    /// Gets a value indicating whether the texture uses DX10 extended header.
    /// </summary>
    public bool IsDx10 { get; init; }

    /// <summary>
    /// Gets a value indicating whether the texture is a cubemap.
    /// </summary>
    public bool IsCubemap { get; init; }

    /// <summary>
    /// Gets a value indicating whether the texture is a volume texture.
    /// </summary>
    public bool IsVolume { get; init; }

    /// <summary>
    /// Gets the human-readable pixel format description.
    /// </summary>
    public string PixelFormat { get; init; } = "Unknown";

    /// <summary>
    /// Gets the file size in bytes.
    /// </summary>
    public long FileSize { get; init; }

    /// <summary>
    /// Gets the source file path (if applicable).
    /// </summary>
    public string? FilePath { get; init; }

    /// <summary>
    /// Gets a value indicating whether both dimensions are powers of 2.
    /// </summary>
    public bool IsPowerOf2 => IsPow2(Width) && IsPow2(Height);

    /// <summary>
    /// Gets a value indicating whether dimensions are compatible with block compression (multiples of 4).
    /// </summary>
    public bool IsBCCompatible => Width % 4 == 0 && Height % 4 == 0;

    /// <summary>
    /// Gets the aspect ratio (width / height).
    /// </summary>
    public float AspectRatio => Height > 0 ? (float)Width / Height : 0;

    /// <summary>
    /// Gets the total pixel count including all mipmap levels and depth.
    /// </summary>
    public long TotalPixels
    {
        get
        {
            long pixels = 0;
            int w = Width, h = Height;
            for (int i = 0; i < MipmapCount; i++)
            {
                pixels += w * h;
                w = Math.Max(1, w / 2);
                h = Math.Max(1, h / 2);
            }
            return pixels * Depth;
        }
    }

    private static bool IsPow2(int n) => n > 0 && (n & (n - 1)) == 0;
}

/// <summary>
/// DDS header flags constants.
/// </summary>
[Flags]
public enum DDSFlags : uint
{
    /// <summary>Required in every .dds file.</summary>
    Caps = 0x1,
    /// <summary>Required in every .dds file.</summary>
    Height = 0x2,
    /// <summary>Required in every .dds file.</summary>
    Width = 0x4,
    /// <summary>Required when pitch is provided for an uncompressed texture.</summary>
    Pitch = 0x8,
    /// <summary>Required in every .dds file.</summary>
    PixelFormat = 0x1000,
    /// <summary>Required for mipmapped textures.</summary>
    MipmapCount = 0x20000,
    /// <summary>Required when pitch is provided for a compressed texture.</summary>
    LinearSize = 0x80000,
    /// <summary>Required for depth textures.</summary>
    Depth = 0x800000
}

/// <summary>
/// DDS pixel format flags.
/// </summary>
[Flags]
public enum DDSPixelFlags : uint
{
    /// <summary>Texture contains alpha data.</summary>
    AlphaPixels = 0x1,
    /// <summary>Used in some older DDS files for alpha channel only.</summary>
    Alpha = 0x2,
    /// <summary>Texture contains compressed RGB data.</summary>
    FourCC = 0x4,
    /// <summary>Texture contains uncompressed RGB data.</summary>
    RGB = 0x40,
    /// <summary>Used in some older DDS files for YUV uncompressed data.</summary>
    YUV = 0x200,
    /// <summary>Used in some older DDS files for single channel color uncompressed data.</summary>
    Luminance = 0x20000
}

/// <summary>
/// Result of DDS texture validation.
/// </summary>
public record DDSValidationResult
{
    /// <summary>
    /// Gets a value indicating whether the texture passed all validation checks.
    /// </summary>
    public bool IsValid => Issues.Count == 0;

    /// <summary>
    /// Gets the list of validation issues found.
    /// </summary>
    public IReadOnlyList<string> Issues { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the list of warnings (non-critical issues).
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Result of analyzing textures in a BA2 archive.
/// </summary>
public record ArchiveTextureAnalysisResult
{
    /// <summary>
    /// Gets the archive file path.
    /// </summary>
    public string ArchivePath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the archive file name.
    /// </summary>
    public string ArchiveName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the total number of textures analyzed.
    /// </summary>
    public int TotalTextures { get; init; }

    /// <summary>
    /// Gets the list of texture dimension issues (odd dimensions).
    /// </summary>
    public IReadOnlyList<TextureDimensionIssue> DimensionIssues { get; init; } = Array.Empty<TextureDimensionIssue>();

    /// <summary>
    /// Gets the list of texture format issues (non-DDS textures).
    /// </summary>
    public IReadOnlyList<TextureFormatIssue> FormatIssues { get; init; } = Array.Empty<TextureFormatIssue>();
}
