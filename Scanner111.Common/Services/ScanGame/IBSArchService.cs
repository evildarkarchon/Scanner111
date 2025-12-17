using Scanner111.Common.Models.ScanGame;

namespace Scanner111.Common.Services.ScanGame;

/// <summary>
/// Provides functionality to interact with BSArch.exe for BA2 archive operations.
/// </summary>
/// <remarks>
/// BSArch is an external tool for working with Bethesda archive files.
/// This service provides methods for:
/// - Listing files in archives (-list)
/// - Dumping texture information (-dump)
/// - Parsing BSArch output for texture analysis
/// </remarks>
public interface IBSArchService
{
    /// <summary>
    /// Gets or sets the path to the BSArch executable.
    /// </summary>
    string? BSArchPath { get; set; }

    /// <summary>
    /// Gets a value indicating whether BSArch is available and configured.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Attempts to find BSArch in common locations.
    /// </summary>
    /// <returns>True if BSArch was found, false otherwise.</returns>
    Task<bool> TryLocateBSArchAsync();

    /// <summary>
    /// Lists all files in a BA2 archive using BSArch -list.
    /// </summary>
    /// <param name="archivePath">Path to the BA2 archive.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>List of file paths within the archive.</returns>
    Task<IReadOnlyList<string>> ListArchiveContentsAsync(
        string archivePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Dumps texture information from a texture BA2 archive using BSArch -dump.
    /// </summary>
    /// <param name="archivePath">Path to the BA2 archive.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>Analysis result containing texture issues.</returns>
    Task<ArchiveTextureAnalysisResult> DumpTextureInfoAsync(
        string archivePath,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents information about a file within a BA2 archive from BSArch output.
/// </summary>
/// <param name="FilePath">The path of the file within the archive.</param>
/// <param name="Extension">The file extension (e.g., "dds", "nif").</param>
public record ArchiveFileInfo(string FilePath, string Extension);

/// <summary>
/// Represents texture information parsed from BSArch -dump output.
/// </summary>
/// <param name="TexturePath">The path of the texture within the archive.</param>
/// <param name="Extension">The file extension.</param>
/// <param name="Width">Texture width in pixels.</param>
/// <param name="Height">Texture height in pixels.</param>
public record ArchiveTextureInfo(string TexturePath, string Extension, int Width, int Height);
