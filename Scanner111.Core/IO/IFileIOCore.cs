using System.Text;

namespace Scanner111.Core.IO;

/// <summary>
/// Defines async-first file I/O operations with automatic encoding detection.
/// </summary>
public interface IFileIoCore
{
    /// <summary>
    /// Read entire file contents with automatic encoding detection.
    /// </summary>
    /// <param name="path">Path to the file to read.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The contents of the file as a string.</returns>
    Task<string> ReadFileAsync(string path, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Read all lines from a file with automatic encoding detection.
    /// </summary>
    /// <param name="path">Path to the file to read.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>An array of lines from the file.</returns>
    Task<string[]> ReadLinesAsync(string path, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Write content to a file with specified encoding.
    /// </summary>
    /// <param name="path">Path to the file to write.</param>
    /// <param name="content">Content to write to the file.</param>
    /// <param name="encoding">Encoding to use (defaults to UTF-8 if null).</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task WriteFileAsync(string path, string content, Encoding? encoding = null, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if a file exists.
    /// </summary>
    /// <param name="path">Path to the file to check.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True if the file exists, false otherwise.</returns>
    Task<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get file modification time.
    /// </summary>
    /// <param name="path">Path to the file.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The last write time of the file, or null if file doesn't exist.</returns>
    Task<DateTime?> GetLastWriteTimeAsync(string path, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Create a directory if it doesn't exist.
    /// </summary>
    /// <param name="path">Path to the directory to create.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete a file if it exists.
    /// </summary>
    /// <param name="path">Path to the file to delete.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True if the file was deleted, false if it didn't exist.</returns>
    Task<bool> DeleteFileAsync(string path, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Copy a file from source to destination.
    /// </summary>
    /// <param name="sourcePath">Source file path.</param>
    /// <param name="destinationPath">Destination file path.</param>
    /// <param name="overwrite">Whether to overwrite the destination if it exists.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task CopyFileAsync(string sourcePath, string destinationPath, bool overwrite = false,
        CancellationToken cancellationToken = default);
}