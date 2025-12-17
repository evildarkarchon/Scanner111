using Scanner111.Common.Models.ScanGame;

namespace Scanner111.Common.Services.ScanGame;

/// <summary>
/// Provides functionality for scanning unpacked (loose) mod files in a directory.
/// </summary>
/// <remarks>
/// This scanner detects various issues with unpacked mod files including:
/// <list type="bullet">
/// <item>Cleanup items (readme files, FOMOD folders) that could be removed</item>
/// <item>Animation data directories that may cause conflicts</item>
/// <item>Texture format issues (TGA/PNG files that should be DDS)</item>
/// <item>Texture dimension issues (odd dimensions in DDS files)</item>
/// <item>Sound format issues (MP3/M4A files that should be XWM/WAV)</item>
/// <item>XSE script files in loose form (should typically be in BA2)</item>
/// <item>Previs/precombine files that may override game precombines</item>
/// </list>
/// </remarks>
public interface IUnpackedModsScanner
{
    /// <summary>
    /// Scans a directory for unpacked mod files and detects issues.
    /// </summary>
    /// <param name="modPath">The path to the mod directory to scan.</param>
    /// <param name="xseScriptFiles">Dictionary of XSE script file names to detect (e.g., "f4se.dll" -&gt; "F4SE").</param>
    /// <param name="analyzeDdsTextures">Whether to analyze DDS files for dimension issues (slower but more thorough).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An <see cref="UnpackedScanResult"/> containing all detected issues.</returns>
    Task<UnpackedScanResult> ScanAsync(
        string modPath,
        IReadOnlyDictionary<string, string>? xseScriptFiles = null,
        bool analyzeDdsTextures = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Scans a directory for unpacked mod files with progress reporting.
    /// </summary>
    /// <param name="modPath">The path to the mod directory to scan.</param>
    /// <param name="xseScriptFiles">Dictionary of XSE script file names to detect.</param>
    /// <param name="analyzeDdsTextures">Whether to analyze DDS files for dimension issues.</param>
    /// <param name="progress">Progress reporter for scan progress updates.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An <see cref="UnpackedScanResult"/> containing all detected issues.</returns>
    Task<UnpackedScanResult> ScanWithProgressAsync(
        string modPath,
        IReadOnlyDictionary<string, string>? xseScriptFiles,
        bool analyzeDdsTextures,
        IProgress<UnpackedScanProgress>? progress,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents progress information during an unpacked mods scan.
/// </summary>
/// <param name="CurrentDirectory">The directory currently being scanned.</param>
/// <param name="DirectoriesScanned">Number of directories scanned so far.</param>
/// <param name="FilesScanned">Number of files scanned so far.</param>
/// <param name="IssuesFound">Number of issues found so far.</param>
public record UnpackedScanProgress(
    string CurrentDirectory,
    int DirectoriesScanned,
    int FilesScanned,
    int IssuesFound);
