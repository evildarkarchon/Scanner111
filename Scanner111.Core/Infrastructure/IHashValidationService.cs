using Scanner111.Core.Models;

namespace Scanner111.Core.Infrastructure;

/// <summary>
///     Service for calculating and validating file hashes
/// </summary>
public interface IHashValidationService
{
    /// <summary>
    ///     Calculate the SHA256 hash of a file
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Hex string representation of the hash</returns>
    Task<string> CalculateFileHashAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Validate a file against an expected hash
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <param name="expectedHash">Expected hash value</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result</returns>
    Task<HashValidation> ValidateFileAsync(string filePath, string expectedHash,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Validate multiple files in batch
    /// </summary>
    /// <param name="fileHashMap">Dictionary of file paths to expected hashes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary of file paths to validation results</returns>
    Task<Dictionary<string, HashValidation>> ValidateBatchAsync(Dictionary<string, string> fileHashMap,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Calculate hash with progress reporting
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <param name="progress">Progress reporter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Hex string representation of the hash</returns>
    Task<string> CalculateFileHashWithProgressAsync(string filePath, IProgress<long>? progress,
        CancellationToken cancellationToken = default);
}