using Scanner111.Core.Models;

namespace Scanner111.Core.Discovery;

/// <summary>
///     Service for validating file system paths with thread-safe operations.
/// </summary>
public interface IPathValidationService
{
    /// <summary>
    ///     Validates a path asynchronously with comprehensive checks.
    /// </summary>
    /// <param name="path">The path to validate.</param>
    /// <param name="checkRead">Whether to check read permissions.</param>
    /// <param name="checkWrite">Whether to check write permissions.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The validation result.</returns>
    Task<PathValidationResult> ValidatePathAsync(
        string path,
        bool checkRead = true,
        bool checkWrite = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Validates multiple paths concurrently.
    /// </summary>
    /// <param name="paths">The paths to validate.</param>
    /// <param name="checkRead">Whether to check read permissions.</param>
    /// <param name="checkWrite">Whether to check write permissions.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary of paths to their validation results.</returns>
    Task<Dictionary<string, PathValidationResult>> ValidatePathsAsync(
        IEnumerable<string> paths,
        bool checkRead = true,
        bool checkWrite = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks if a file exists and is accessible.
    /// </summary>
    /// <param name="filePath">The file path to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the file exists and is accessible.</returns>
    Task<bool> FileExistsAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks if a directory exists and is accessible.
    /// </summary>
    /// <param name="directoryPath">The directory path to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the directory exists and is accessible.</returns>
    Task<bool> DirectoryExistsAsync(string directoryPath, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks if the current process has read access to a path.
    /// </summary>
    /// <param name="path">The path to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if read access is available.</returns>
    Task<bool> HasReadAccessAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks if the current process has write access to a path.
    /// </summary>
    /// <param name="path">The path to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if write access is available.</returns>
    Task<bool> HasWriteAccessAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Normalizes a path to use consistent separators and resolve relative segments.
    /// </summary>
    /// <param name="path">The path to normalize.</param>
    /// <returns>The normalized path.</returns>
    string NormalizePath(string path);

    /// <summary>
    ///     Validates that a path is safe (no directory traversal, etc.).
    /// </summary>
    /// <param name="path">The path to validate.</param>
    /// <param name="basePath">Optional base path to ensure the path is within.</param>
    /// <returns>True if the path is safe.</returns>
    bool IsPathSafe(string path, string? basePath = null);

    /// <summary>
    ///     Gets cached validation result if available and not expired.
    /// </summary>
    /// <param name="path">The path to get cached result for.</param>
    /// <returns>Cached result or null if not available or expired.</returns>
    PathValidationResult? GetCachedResult(string path);

    /// <summary>
    ///     Clears the validation cache.
    /// </summary>
    void ClearCache();

    /// <summary>
    ///     Sets the cache expiration time.
    /// </summary>
    /// <param name="expiration">The cache expiration timespan.</param>
    void SetCacheExpiration(TimeSpan expiration);
}