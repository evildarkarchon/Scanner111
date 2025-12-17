using Scanner111.Common.Models.ScanGame;

namespace Scanner111.Common.Services.ScanGame;

/// <summary>
/// Provides functionality for scanning and validating Bethesda BA2 archive files.
/// </summary>
/// <remarks>
/// BA2 archives are the primary mod archive format for Fallout 4.
/// This scanner validates archive headers, detects texture issues (odd dimensions,
/// non-DDS formats), identifies incorrect sound formats (MP3/M4A instead of XWM),
/// and detects XSE script files that should not be packed in archives.
/// </remarks>
public interface IBA2Scanner
{
    /// <summary>
    /// Scans a directory for BA2 archive files and validates their contents.
    /// </summary>
    /// <param name="modPath">The path to the directory to scan (typically the game's Data folder or mod directory).</param>
    /// <param name="xseScriptFolders">Dictionary of XSE script folder names to detect (e.g., "f4se").</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="BA2ScanResult"/> containing all detected issues.</returns>
    Task<BA2ScanResult> ScanAsync(
        string modPath,
        IReadOnlyDictionary<string, string>? xseScriptFolders = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds all BA2 files in a directory recursively.
    /// </summary>
    /// <param name="modPath">The path to the directory to search.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of BA2 file paths.</returns>
    Task<IReadOnlyList<string>> FindBA2FilesAsync(
        string modPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads and validates the header of a BA2 archive.
    /// </summary>
    /// <param name="archivePath">The path to the BA2 archive file.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>Header information including validity, format type, and version.</returns>
    Task<BA2HeaderInfo> ReadHeaderAsync(
        string archivePath,
        CancellationToken cancellationToken = default);
}
