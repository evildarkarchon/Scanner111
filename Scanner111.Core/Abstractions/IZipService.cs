using Scanner111.Core.Models;

namespace Scanner111.Core.Abstractions;

/// <summary>
/// Abstraction for zip file operations
/// </summary>
public interface IZipService
{
    /// <summary>
    /// Create a new zip archive and add files to it
    /// </summary>
    /// <param name="zipPath">Path where the zip file will be created</param>
    /// <param name="files">Dictionary of source file paths to entry names in the zip</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> CreateZipAsync(string zipPath, Dictionary<string, string> files, CancellationToken cancellationToken = default);

    /// <summary>
    /// Add a single file to a zip archive
    /// </summary>
    /// <param name="zipPath">Path to the zip file</param>
    /// <param name="sourceFile">Path to the file to add</param>
    /// <param name="entryName">Name of the entry in the zip</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> AddFileToZipAsync(string zipPath, string sourceFile, string entryName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extract files from a zip archive
    /// </summary>
    /// <param name="zipPath">Path to the zip file</param>
    /// <param name="targetDirectory">Directory to extract files to</param>
    /// <param name="filesToExtract">Optional list of specific files to extract (null extracts all)</param>
    /// <param name="overwrite">Whether to overwrite existing files</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of extracted file paths</returns>
    Task<IEnumerable<string>> ExtractZipAsync(string zipPath, string targetDirectory, IEnumerable<string>? filesToExtract = null, bool overwrite = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extract a single file from a zip archive
    /// </summary>
    /// <param name="zipPath">Path to the zip file</param>
    /// <param name="entryName">Name of the entry to extract</param>
    /// <param name="targetPath">Path where the file should be extracted</param>
    /// <param name="overwrite">Whether to overwrite if the file exists</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> ExtractFileFromZipAsync(string zipPath, string entryName, string targetPath, bool overwrite = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// List all entries in a zip archive
    /// </summary>
    /// <param name="zipPath">Path to the zip file</param>
    /// <returns>List of entry names in the zip</returns>
    Task<IEnumerable<string>> ListZipEntriesAsync(string zipPath);

    /// <summary>
    /// Get the count of entries in a zip archive
    /// </summary>
    /// <param name="zipPath">Path to the zip file</param>
    /// <returns>Number of entries in the zip</returns>
    Task<int> GetZipEntryCountAsync(string zipPath);

    /// <summary>
    /// Check if a zip file exists and is valid
    /// </summary>
    /// <param name="zipPath">Path to the zip file</param>
    /// <returns>True if the zip exists and is valid</returns>
    Task<bool> IsValidZipAsync(string zipPath);
}