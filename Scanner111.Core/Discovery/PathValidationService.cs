using System.Collections.Concurrent;
using System.Security;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Models;

namespace Scanner111.Core.Discovery;

/// <summary>
///     Thread-safe service for validating file system paths with caching.
/// </summary>
public sealed class PathValidationService : IPathValidationService, IDisposable
{
    private readonly ConcurrentDictionary<string, CachedValidationResult> _cache;
    private readonly ILogger<PathValidationService> _logger;
    private readonly SemaphoreSlim _validationSemaphore;
    private TimeSpan _cacheExpiration;

    public PathValidationService(ILogger<PathValidationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = new ConcurrentDictionary<string, CachedValidationResult>(StringComparer.OrdinalIgnoreCase);
        _validationSemaphore = new SemaphoreSlim(10, 10); // Allow up to 10 concurrent validations
        _cacheExpiration = TimeSpan.FromMinutes(2);
    }

    public void Dispose()
    {
        _validationSemaphore?.Dispose();
    }

    public async Task<PathValidationResult> ValidatePathAsync(
        string path,
        bool checkRead = true,
        bool checkWrite = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path)) return PathValidationResult.Failure(path, "Path is null or empty");

        // Check cache first
        var cached = GetCachedResult(path);
        if (cached != null)
        {
            _logger.LogDebug("Returning cached validation result for {Path}", path);
            return cached;
        }

        await _validationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Double-check cache after acquiring semaphore
            cached = GetCachedResult(path);
            if (cached != null) return cached;

            var normalizedPath = NormalizePath(path);
            var issues = new List<string>();

            // Check if path exists
            var exists = File.Exists(normalizedPath) || Directory.Exists(normalizedPath);
            if (!exists)
            {
                var result = PathValidationResult.Failure(normalizedPath,
                    $"Path does not exist: {normalizedPath}", issues);
                CacheResult(path, result);
                return result;
            }

            var canRead = true;
            var canWrite = true;

            // Check read access if requested
            if (checkRead)
            {
                canRead = await HasReadAccessAsync(normalizedPath, cancellationToken).ConfigureAwait(false);
                if (!canRead) issues.Add("No read access to path");
            }

            // Check write access if requested
            if (checkWrite)
            {
                canWrite = await HasWriteAccessAsync(normalizedPath, cancellationToken).ConfigureAwait(false);
                if (!canWrite) issues.Add("No write access to path");
            }

            if (issues.Count > 0)
            {
                var result = PathValidationResult.Failure(normalizedPath,
                    "Path validation failed", issues);
                CacheResult(path, result);
                return result;
            }

            var successResult = PathValidationResult.Success(normalizedPath, canRead, canWrite);
            CacheResult(path, successResult);

            _logger.LogDebug("Successfully validated path {Path}", normalizedPath);
            return successResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating path {Path}", path);
            return PathValidationResult.Failure(path,
                $"Exception during validation: {ex.Message}");
        }
        finally
        {
            _validationSemaphore.Release();
        }
    }

    public async Task<Dictionary<string, PathValidationResult>> ValidatePathsAsync(
        IEnumerable<string> paths,
        bool checkRead = true,
        bool checkWrite = false,
        CancellationToken cancellationToken = default)
    {
        var pathList = paths?.ToList() ?? throw new ArgumentNullException(nameof(paths));

        var tasks = pathList.Select(async path =>
        {
            var result = await ValidatePathAsync(path, checkRead, checkWrite, cancellationToken)
                .ConfigureAwait(false);
            return (path, result);
        });

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        return results.ToDictionary(r => r.path, r => r.result, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<bool> FileExistsAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return false;

        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return File.Exists(NormalizePath(filePath));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking file existence for {FilePath}", filePath);
                return false;
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> DirectoryExistsAsync(string directoryPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(directoryPath)) return false;

        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return Directory.Exists(NormalizePath(directoryPath));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking directory existence for {DirectoryPath}", directoryPath);
                return false;
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> HasReadAccessAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var normalizedPath = NormalizePath(path);

                if (File.Exists(normalizedPath))
                {
                    // Try to open file for reading
                    using var stream = File.Open(normalizedPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    return true;
                }

                if (Directory.Exists(normalizedPath))
                {
                    // Try to enumerate directory
                    _ = Directory.GetFiles(normalizedPath).FirstOrDefault();
                    return true;
                }

                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (SecurityException)
            {
                return false;
            }
            catch (IOException)
            {
                // File might be in use, but we have read access
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking read access for {Path}", path);
                return false;
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> HasWriteAccessAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var normalizedPath = NormalizePath(path);

                if (File.Exists(normalizedPath))
                {
                    // Check file attributes
                    var attributes = File.GetAttributes(normalizedPath);
                    if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly) return false;

                    // Try to open file for writing
                    using var stream = File.Open(normalizedPath, FileMode.Open, FileAccess.Write, FileShare.Read);
                    return true;
                }

                if (Directory.Exists(normalizedPath))
                {
                    // Try to create a temp file in the directory
                    var tempFile = Path.Combine(normalizedPath, $".scanner111_test_{Guid.NewGuid()}.tmp");
                    try
                    {
                        File.WriteAllText(tempFile, "test");
                        File.Delete(tempFile);
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                }

                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (SecurityException)
            {
                return false;
            }
            catch (IOException)
            {
                // File might be in use
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking write access for {Path}", path);
                return false;
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    public string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;

        try
        {
            // Get full path (resolves relative paths)
            var fullPath = Path.GetFullPath(path);

            // Normalize directory separators
            fullPath = fullPath.Replace('/', Path.DirectorySeparatorChar);
            fullPath = fullPath.Replace('\\', Path.DirectorySeparatorChar);

            // Remove trailing separator for directories (except root)
            if (fullPath.Length > 1 && fullPath.EndsWith(Path.DirectorySeparatorChar))
            {
                var root = Path.GetPathRoot(fullPath);
                if (fullPath != root) fullPath = fullPath.TrimEnd(Path.DirectorySeparatorChar);
            }

            return fullPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error normalizing path {Path}", path);
            return path;
        }
    }

    public bool IsPathSafe(string path, string? basePath = null)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        try
        {
            var normalizedPath = NormalizePath(path);

            // Check for directory traversal attempts
            if (normalizedPath.Contains("..", StringComparison.Ordinal)) return false;

            // If base path is provided, ensure the path is within it
            if (!string.IsNullOrWhiteSpace(basePath))
            {
                var normalizedBase = NormalizePath(basePath);
                if (!normalizedPath.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase)) return false;
            }

            // Check for invalid characters
            var invalidChars = Path.GetInvalidPathChars();
            if (path.IndexOfAny(invalidChars) >= 0) return false;

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking path safety for {Path}", path);
            return false;
        }
    }

    public PathValidationResult? GetCachedResult(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || _cacheExpiration == TimeSpan.Zero) return null;

        var normalizedPath = NormalizePath(path);

        if (_cache.TryGetValue(normalizedPath, out var cached))
        {
            if (DateTimeOffset.UtcNow - cached.CachedAt < _cacheExpiration) return cached.Result;

            // Remove expired entry
            _cache.TryRemove(normalizedPath, out _);
        }

        return null;
    }

    public void ClearCache()
    {
        _cache.Clear();
        _logger.LogDebug("Path validation cache cleared");
    }

    public void SetCacheExpiration(TimeSpan expiration)
    {
        _cacheExpiration = expiration;
        _logger.LogDebug("Cache expiration set to {Expiration}", expiration);

        if (expiration == TimeSpan.Zero) ClearCache();
    }

    private void CacheResult(string path, PathValidationResult result)
    {
        if (_cacheExpiration > TimeSpan.Zero)
        {
            var normalizedPath = NormalizePath(path);
            _cache[normalizedPath] = new CachedValidationResult(result, DateTimeOffset.UtcNow);
        }
    }

    private sealed record CachedValidationResult(PathValidationResult Result, DateTimeOffset CachedAt);
}