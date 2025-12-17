namespace Scanner111.Common.Models.ScanGame;

/// <summary>
/// Represents the result of scanning BA2 archive files.
/// </summary>
public record BA2ScanResult
{
    /// <summary>
    /// Gets the total number of BA2 files scanned.
    /// </summary>
    public int TotalFilesScanned { get; init; }

    /// <summary>
    /// Gets the list of BA2 format issues (invalid header signatures).
    /// </summary>
    public IReadOnlyList<BA2FormatIssue> FormatIssues { get; init; } = Array.Empty<BA2FormatIssue>();

    /// <summary>
    /// Gets the list of texture dimension issues (odd-numbered dimensions).
    /// </summary>
    public IReadOnlyList<TextureDimensionIssue> TextureDimensionIssues { get; init; } = Array.Empty<TextureDimensionIssue>();

    /// <summary>
    /// Gets the list of texture format issues (non-DDS textures in texture archives).
    /// </summary>
    public IReadOnlyList<TextureFormatIssue> TextureFormatIssues { get; init; } = Array.Empty<TextureFormatIssue>();

    /// <summary>
    /// Gets the list of sound format issues (MP3/M4A instead of XWM).
    /// </summary>
    public IReadOnlyList<SoundFormatIssue> SoundFormatIssues { get; init; } = Array.Empty<SoundFormatIssue>();

    /// <summary>
    /// Gets the list of BA2 files containing XSE script files.
    /// </summary>
    public IReadOnlyList<XseFileIssue> XseFileIssues { get; init; } = Array.Empty<XseFileIssue>();

    /// <summary>
    /// Gets a value indicating whether any issues were found.
    /// </summary>
    public bool HasIssues =>
        FormatIssues.Count > 0 ||
        TextureDimensionIssues.Count > 0 ||
        TextureFormatIssues.Count > 0 ||
        SoundFormatIssues.Count > 0 ||
        XseFileIssues.Count > 0;
}

/// <summary>
/// Represents a BA2 file with an invalid format header.
/// </summary>
/// <param name="ArchivePath">The path to the BA2 archive.</param>
/// <param name="ArchiveName">The name of the BA2 archive.</param>
/// <param name="HeaderBytes">The header bytes that were read (for diagnostics).</param>
public record BA2FormatIssue(string ArchivePath, string ArchiveName, string HeaderBytes);

/// <summary>
/// Represents a texture with odd-numbered dimensions.
/// </summary>
/// <param name="ArchiveName">The name of the BA2 archive containing the texture.</param>
/// <param name="TexturePath">The path to the texture within the archive.</param>
/// <param name="Width">The texture width.</param>
/// <param name="Height">The texture height.</param>
public record TextureDimensionIssue(string ArchiveName, string TexturePath, int Width, int Height);

/// <summary>
/// Represents a non-DDS texture in a texture archive.
/// </summary>
/// <param name="ArchiveName">The name of the BA2 archive containing the texture.</param>
/// <param name="TexturePath">The path to the texture within the archive.</param>
/// <param name="Extension">The file extension of the non-DDS texture.</param>
public record TextureFormatIssue(string ArchiveName, string TexturePath, string Extension);

/// <summary>
/// Represents a sound file using incorrect format (MP3/M4A instead of XWM).
/// </summary>
/// <param name="ArchiveName">The name of the BA2 archive containing the sound.</param>
/// <param name="SoundPath">The path to the sound file within the archive.</param>
/// <param name="Extension">The file extension (mp3 or m4a).</param>
public record SoundFormatIssue(string ArchiveName, string SoundPath, string Extension);

/// <summary>
/// Represents a BA2 archive containing XSE script files.
/// </summary>
/// <param name="ArchivePath">The path to the BA2 archive.</param>
/// <param name="ArchiveName">The name of the BA2 archive.</param>
public record XseFileIssue(string ArchivePath, string ArchiveName);

/// <summary>
/// Represents the format type of a BA2 archive.
/// </summary>
public enum BA2Format
{
    /// <summary>
    /// Unknown or invalid format.
    /// </summary>
    Unknown,

    /// <summary>
    /// General format (GNRL) for meshes, scripts, sounds, etc.
    /// </summary>
    General,

    /// <summary>
    /// Texture format (DX10) for DDS textures.
    /// </summary>
    Texture
}

/// <summary>
/// Represents information about a BA2 archive header.
/// </summary>
/// <param name="IsValid">Whether the header is valid.</param>
/// <param name="Format">The archive format type.</param>
/// <param name="Version">The archive version.</param>
public record BA2HeaderInfo(bool IsValid, BA2Format Format, uint Version);
