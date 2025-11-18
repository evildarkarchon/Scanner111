namespace Scanner111.Common.Services.FileIO;

/// <summary>
/// Provides abstraction for file I/O operations.
/// </summary>
public interface IFileIOService
{
    /// <summary>
    /// Reads the entire contents of a file asynchronously.
    /// </summary>
    /// <param name="path">The absolute path to the file to read.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The contents of the file as a string.</returns>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="UnauthorizedAccessException">The caller does not have permission to read the file.</exception>
    Task<string> ReadFileAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes content to a file asynchronously.
    /// </summary>
    /// <param name="path">The absolute path to the file to write.</param>
    /// <param name="content">The content to write to the file.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="UnauthorizedAccessException">The caller does not have permission to write the file.</exception>
    Task WriteFileAsync(string path, string content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a file exists asynchronously.
    /// </summary>
    /// <param name="path">The absolute path to the file to check.</param>
    /// <returns>True if the file exists; otherwise, false.</returns>
    Task<bool> FileExistsAsync(string path);
}
