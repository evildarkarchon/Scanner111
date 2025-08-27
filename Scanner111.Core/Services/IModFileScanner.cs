namespace Scanner111.Core.Services;

/// <summary>
///     Service for scanning mod files to detect potential issues with textures, sounds, 
///     scripts, and other mod files. Provides functionality equivalent to Python's ScanGameCore.
/// </summary>
public interface IModFileScanner
{
    /// <summary>
    ///     Scans unpacked/loose mod files for potential issues.
    /// </summary>
    /// <param name="modPath">Path to the mods directory</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Detailed report of scan results</returns>
    Task<string> ScanModsUnpackedAsync(string modPath, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Scans archived BA2 mod files for potential issues.
    /// </summary>
    /// <param name="modPath">Path to the mods directory</param>
    /// <param name="bsArchPath">Path to BSArch.exe tool</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Detailed report of scan results</returns>
    Task<string> ScanModsArchivedAsync(string modPath, string bsArchPath, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks log files within a specified folder for recorded errors.
    /// </summary>
    /// <param name="folderPath">Path to the folder containing log files</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Detailed report of all detected errors in log files</returns>
    Task<string> CheckLogErrorsAsync(string folderPath, CancellationToken cancellationToken = default);
}