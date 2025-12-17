namespace Scanner111.Common.Models.ScanGame;

/// <summary>
/// Represents the result of scanning unpacked (loose) mod files in a directory.
/// </summary>
public record UnpackedScanResult
{
    /// <summary>
    /// Gets the total number of directories scanned.
    /// </summary>
    public int TotalDirectoriesScanned { get; init; }

    /// <summary>
    /// Gets the total number of files scanned.
    /// </summary>
    public int TotalFilesScanned { get; init; }

    /// <summary>
    /// Gets the list of cleanup items found (readme files, FOMOD folders).
    /// </summary>
    public IReadOnlyList<CleanupIssue> CleanupIssues { get; init; } = Array.Empty<CleanupIssue>();

    /// <summary>
    /// Gets the list of directories containing animation data.
    /// </summary>
    public IReadOnlyList<AnimationDataIssue> AnimationDataIssues { get; init; } = Array.Empty<AnimationDataIssue>();

    /// <summary>
    /// Gets the list of texture format issues (TGA/PNG not converted to DDS).
    /// </summary>
    public IReadOnlyList<UnpackedTextureFormatIssue> TextureFormatIssues { get; init; } = Array.Empty<UnpackedTextureFormatIssue>();

    /// <summary>
    /// Gets the list of texture dimension issues found in loose DDS files.
    /// </summary>
    public IReadOnlyList<UnpackedTextureDimensionIssue> TextureDimensionIssues { get; init; } = Array.Empty<UnpackedTextureDimensionIssue>();

    /// <summary>
    /// Gets the list of sound format issues (MP3/M4A not converted to XWM/WAV).
    /// </summary>
    public IReadOnlyList<UnpackedSoundFormatIssue> SoundFormatIssues { get; init; } = Array.Empty<UnpackedSoundFormatIssue>();

    /// <summary>
    /// Gets the list of directories containing XSE script files.
    /// </summary>
    public IReadOnlyList<UnpackedXseFileIssue> XseFileIssues { get; init; } = Array.Empty<UnpackedXseFileIssue>();

    /// <summary>
    /// Gets the list of directories containing previs/precombine files.
    /// </summary>
    public IReadOnlyList<PrevisFileIssue> PrevisFileIssues { get; init; } = Array.Empty<PrevisFileIssue>();

    /// <summary>
    /// Gets a value indicating whether any issues were found.
    /// </summary>
    public bool HasIssues =>
        CleanupIssues.Count > 0 ||
        AnimationDataIssues.Count > 0 ||
        TextureFormatIssues.Count > 0 ||
        TextureDimensionIssues.Count > 0 ||
        SoundFormatIssues.Count > 0 ||
        XseFileIssues.Count > 0 ||
        PrevisFileIssues.Count > 0;
}

/// <summary>
/// Represents a cleanup item (readme file or FOMOD folder) that could be removed.
/// </summary>
/// <param name="FilePath">The path to the cleanup item.</param>
/// <param name="RelativePath">The path relative to the mod directory.</param>
/// <param name="ItemType">The type of cleanup item (file or folder).</param>
public record CleanupIssue(string FilePath, string RelativePath, CleanupItemType ItemType);

/// <summary>
/// Represents a directory containing animation data files.
/// </summary>
/// <param name="DirectoryPath">The path to the directory containing AnimationFileData.</param>
/// <param name="RelativePath">The path relative to the mod directory.</param>
public record AnimationDataIssue(string DirectoryPath, string RelativePath);

/// <summary>
/// Represents a texture file in an incorrect format (TGA/PNG instead of DDS).
/// </summary>
/// <param name="FilePath">The full path to the texture file.</param>
/// <param name="RelativePath">The path relative to the mod directory.</param>
/// <param name="Extension">The file extension (tga or png).</param>
public record UnpackedTextureFormatIssue(string FilePath, string RelativePath, string Extension);

/// <summary>
/// Represents a loose DDS texture with dimension issues.
/// </summary>
/// <param name="FilePath">The full path to the texture file.</param>
/// <param name="RelativePath">The path relative to the mod directory.</param>
/// <param name="Width">The texture width.</param>
/// <param name="Height">The texture height.</param>
/// <param name="Issue">Description of the dimension issue.</param>
public record UnpackedTextureDimensionIssue(string FilePath, string RelativePath, int Width, int Height, string Issue);

/// <summary>
/// Represents a sound file in an incorrect format (MP3/M4A instead of XWM/WAV).
/// </summary>
/// <param name="FilePath">The full path to the sound file.</param>
/// <param name="RelativePath">The path relative to the mod directory.</param>
/// <param name="Extension">The file extension (mp3 or m4a).</param>
public record UnpackedSoundFormatIssue(string FilePath, string RelativePath, string Extension);

/// <summary>
/// Represents a directory containing XSE script files.
/// </summary>
/// <param name="DirectoryPath">The path to the directory containing XSE scripts.</param>
/// <param name="RelativePath">The path relative to the mod directory.</param>
public record UnpackedXseFileIssue(string DirectoryPath, string RelativePath);

/// <summary>
/// Represents a directory containing previs/precombine files.
/// </summary>
/// <param name="DirectoryPath">The path to the directory containing previs files.</param>
/// <param name="RelativePath">The path relative to the mod directory.</param>
public record PrevisFileIssue(string DirectoryPath, string RelativePath);

/// <summary>
/// The type of cleanup item found.
/// </summary>
public enum CleanupItemType
{
    /// <summary>
    /// A readme, changelog, or similar text file.
    /// </summary>
    ReadmeFile,

    /// <summary>
    /// A FOMOD folder (used by mod installers).
    /// </summary>
    FomodFolder
}
