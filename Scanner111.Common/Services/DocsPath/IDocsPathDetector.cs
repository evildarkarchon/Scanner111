using Scanner111.Common.Models.DocsPath;
using Scanner111.Common.Models.GamePath;

namespace Scanner111.Common.Services.DocsPath;

/// <summary>
/// Provides methods for detecting and managing game documents paths.
/// </summary>
/// <remarks>
/// <para>
/// This service locates game documents folders (e.g., Documents/My Games/Fallout4)
/// using multiple detection strategies:
/// </para>
/// <list type="bullet">
/// <item><description>Cached/configured path from user settings</description></item>
/// <item><description>Windows Registry (Shell Folders key)</description></item>
/// <item><description>Environment.SpecialFolder.MyDocuments fallback</description></item>
/// </list>
/// <para>
/// All operations are read-only and never modify the registry or file system.
/// </para>
/// </remarks>
public interface IDocsPathDetector
{
    /// <summary>
    /// Detects the documents path for a specific game type using all available methods.
    /// </summary>
    /// <param name="gameType">The type of game to detect.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="DocsPathResult"/> containing the detection result.</returns>
    /// <remarks>
    /// Detection is attempted in the following order:
    /// <list type="number">
    /// <item><description>Windows Registry Shell Folders key</description></item>
    /// <item><description>Environment.SpecialFolder.MyDocuments fallback</description></item>
    /// </list>
    /// </remarks>
    Task<DocsPathResult> DetectDocsPathAsync(
        GameType gameType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Detects the documents path, checking cached settings first.
    /// </summary>
    /// <param name="gameType">The type of game to detect.</param>
    /// <param name="cachedPath">Optional cached path to validate first.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="DocsPathResult"/> containing the detection result.</returns>
    /// <remarks>
    /// If <paramref name="cachedPath"/> is provided and valid, it is returned immediately
    /// without performing other detection methods.
    /// </remarks>
    Task<DocsPathResult> DetectDocsPathAsync(
        GameType gameType,
        string? cachedPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates derived document paths based on the root documents folder.
    /// </summary>
    /// <param name="gameType">The type of game.</param>
    /// <param name="docsRootPath">The root documents folder path.</param>
    /// <returns>A <see cref="GeneratedDocsPaths"/> containing all derived paths.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="docsRootPath"/> is null or whitespace.</exception>
    GeneratedDocsPaths GeneratePaths(GameType gameType, string docsRootPath);

    /// <summary>
    /// Validates that a path is a valid game documents directory.
    /// </summary>
    /// <param name="gameType">The type of game to validate.</param>
    /// <param name="docsPath">The path to validate.</param>
    /// <returns><c>true</c> if the path is a valid documents folder; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// Validation checks that the directory exists. Unlike game installation paths,
    /// documents folders do not require specific files to be present.
    /// </remarks>
    bool ValidateDocsPath(GameType gameType, string docsPath);

    /// <summary>
    /// Attempts to find the Windows Documents folder from the registry.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The Documents folder path if found, otherwise <c>null</c>.</returns>
    /// <remarks>
    /// This method reads the "Personal" value from the Windows Registry key:
    /// <c>HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders</c>
    /// </remarks>
    Task<string?> FindWindowsDocumentsFolderAsync(CancellationToken cancellationToken = default);
}
