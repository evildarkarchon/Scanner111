using System.Collections.Concurrent;
using System.Security.Cryptography;
using Scanner111.Core.Models;

namespace Scanner111.Core.Infrastructure;

/// <summary>
///     Service for calculating and validating file hashes
/// </summary>
public class HashValidationService : IHashValidationService
{
    private const int BufferSize = 1024 * 1024; // 1MB buffer for reading files
    private readonly ConcurrentDictionary<string, (string hash, DateTime lastModified, long size)> _hashCache = new();
    private readonly ILogger<HashValidationService> _logger;

    public HashValidationService(ILogger<HashValidationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    ///     Calculate the SHA256 hash of a file
    /// </summary>
    public async Task<string> CalculateFileHashAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return await CalculateFileHashWithProgressAsync(filePath, null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Calculate hash with progress reporting
    /// </summary>
    public async Task<string> CalculateFileHashWithProgressAsync(string filePath, IProgress<long>? progress,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException($"File not found: {filePath}");

        // Check cache first
        var fileInfo = new FileInfo(filePath);

        if (_hashCache.TryGetValue(filePath, out var cached))
            // Check if file hasn't been modified
            if (cached.lastModified == fileInfo.LastWriteTimeUtc && cached.size == fileInfo.Length)
            {
                _logger.LogDebug("Using cached hash for {FilePath}", filePath);
                return cached.hash;
            }

        _logger.LogDebug("Calculating hash for {FilePath}", filePath);

        using var sha256 = SHA256.Create();
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, true);

        var buffer = new byte[BufferSize];
        long totalBytesRead = 0;
        int bytesRead;

        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) >
               0)
        {
            sha256.TransformBlock(buffer, 0, bytesRead, null, 0);
            totalBytesRead += bytesRead;
            progress?.Report(totalBytesRead);

            // Check for cancellation
            cancellationToken.ThrowIfCancellationRequested();
        }

        sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        var hash = BitConverter.ToString(sha256.Hash!).Replace("-", "").ToUpperInvariant();

        // Cache the result
        _hashCache[filePath] = (hash, fileInfo.LastWriteTimeUtc, fileInfo.Length);

        return hash;
    }

    /// <summary>
    ///     Validate a file against an expected hash
    /// </summary>
    public async Task<HashValidation> ValidateFileAsync(string filePath, string expectedHash,
        CancellationToken cancellationToken = default)
    {
        var validation = new HashValidation
        {
            FilePath = filePath,
            ExpectedHash = expectedHash,
            HashType = "SHA256"
        };

        try
        {
            if (!File.Exists(filePath))
            {
                validation.ActualHash = string.Empty;
                _logger.LogWarning("File not found for validation: {FilePath}", filePath);
                return validation;
            }

            validation.ActualHash = await CalculateFileHashAsync(filePath, cancellationToken).ConfigureAwait(false);

            if (validation.IsValid)
                _logger.LogDebug("Hash validation passed for {FilePath}", filePath);
            else
                _logger.LogWarning("Hash validation failed for {FilePath}. Expected: {Expected}, Actual: {Actual}",
                    filePath, expectedHash, validation.ActualHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating file {FilePath}", filePath);
            validation.ActualHash = string.Empty;
        }

        return validation;
    }

    /// <summary>
    ///     Validate multiple files in batch
    /// </summary>
    public async Task<Dictionary<string, HashValidation>> ValidateBatchAsync(Dictionary<string, string> fileHashMap,
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, HashValidation>();
        var semaphore = new SemaphoreSlim(Environment.ProcessorCount); // Limit concurrent operations

        var tasks = fileHashMap.Select(async kvp =>
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var validation = await ValidateFileAsync(kvp.Key, kvp.Value, cancellationToken).ConfigureAwait(false);
                lock (results)
                {
                    results[kvp.Key] = validation;
                }
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);

        var validCount = results.Count(r => r.Value.IsValid);
        _logger.LogInformation("Batch validation complete: {Valid}/{Total} files valid", validCount, results.Count);

        return results;
    }
}