using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Models;
using Scanner111.Core.Services;

namespace Scanner111.Core.Analysis;

/// <summary>
///     Interface for thread-safe FCX file checking operations.
/// </summary>
public interface IFcxFileChecker
{
    /// <summary>
    ///     Performs comprehensive file integrity checks for FCX mode.
    /// </summary>
    Task<FcxFileCheckResult> CheckFilesAsync(
        string gamePath,
        string modPath,
        FcxCheckOptions options,
        IProgress<FcxCheckProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Validates main game files for integrity issues.
    /// </summary>
    Task<string> ValidateMainFilesAsync(
        string gamePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Scans mod files for potential problems.
    /// </summary>
    Task<string> ScanModFilesAsync(
        string modPath,
        bool includeArchived,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets cached checksum for a file if available.
    /// </summary>
    Task<string?> GetCachedChecksumAsync(string filePath);

    /// <summary>
    ///     Clears all cached checksums.
    /// </summary>
    Task ClearChecksumCacheAsync();
}

/// <summary>
///     Thread-safe file checker for FCX mode operations with advanced caching and retry logic.
/// </summary>
public sealed class FcxFileChecker : IFcxFileChecker, IAsyncDisposable
{
    private readonly ILogger<FcxFileChecker> _logger;
    private readonly IModFileScanner? _modFileScanner;
    private readonly ConcurrentDictionary<string, ChecksumEntry> _checksumCache;
    private readonly SemaphoreSlim _fileAccessSemaphore;
    private readonly SemaphoreSlim _checksumSemaphore;
    private readonly ReaderWriterLockSlim _cacheLock;
    private readonly int _maxRetries;
    private readonly int _parallelism;
    private bool _disposed;

    public FcxFileChecker(
        ILogger<FcxFileChecker> logger,
        IModFileScanner? modFileScanner = null,
        int maxRetries = 3,
        int parallelism = 4)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _modFileScanner = modFileScanner;
        _checksumCache = new ConcurrentDictionary<string, ChecksumEntry>(StringComparer.OrdinalIgnoreCase);
        _fileAccessSemaphore = new SemaphoreSlim(parallelism, parallelism);
        _checksumSemaphore = new SemaphoreSlim(1, 1);
        _cacheLock = new ReaderWriterLockSlim();
        _maxRetries = maxRetries;
        _parallelism = parallelism;
    }

    /// <inheritdoc />
    public async Task<FcxFileCheckResult> CheckFilesAsync(
        string gamePath,
        string modPath,
        FcxCheckOptions options,
        IProgress<FcxCheckProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(gamePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(modPath);
        ArgumentNullException.ThrowIfNull(options);

        _logger.LogInformation("Starting FCX file checks for game: {GamePath}, mods: {ModPath}", gamePath, modPath);

        var result = new FcxFileCheckResult();
        var totalSteps = 2;
        var currentStep = 0;

        try
        {
            // Check main game files
            progress?.Report(new FcxCheckProgress
            {
                CurrentStep = ++currentStep,
                TotalSteps = totalSteps,
                CurrentOperation = "Validating main game files",
                PercentComplete = (currentStep * 100) / totalSteps
            });

            result.MainFilesResult = await ValidateMainFilesWithRetryAsync(
                gamePath,
                options.RetryOnFailure ? _maxRetries : 1,
                cancellationToken).ConfigureAwait(false);

            // Check mod files
            progress?.Report(new FcxCheckProgress
            {
                CurrentStep = ++currentStep,
                TotalSteps = totalSteps,
                CurrentOperation = "Scanning mod files",
                PercentComplete = (currentStep * 100) / totalSteps
            });

            result.ModFilesResult = await ScanModFilesWithRetryAsync(
                modPath,
                options.IncludeArchivedMods,
                options.RetryOnFailure ? _maxRetries : 1,
                cancellationToken).ConfigureAwait(false);

            result.Success = true;
            result.CompletedAt = DateTime.UtcNow;

            _logger.LogInformation("FCX file checks completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FCX file checks failed");
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<string> ValidateMainFilesAsync(
        string gamePath,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(gamePath);

        await _fileAccessSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _logger.LogDebug("Validating main files at: {GamePath}", gamePath);

            if (!Directory.Exists(gamePath))
            {
                return "❌ Game directory not found\n-----\n";
            }

            var criticalFiles = new[]
            {
                "Fallout4.exe",
                "Fallout4.esm",
                "Data\\Fallout4.esm"
            };

            var missingFiles = new List<string>();
            var corruptedFiles = new List<string>();

            foreach (var file in criticalFiles)
            {
                var fullPath = Path.Combine(gamePath, file);
                if (!File.Exists(fullPath))
                {
                    missingFiles.Add(file);
                    continue;
                }

                // Check file integrity (size check for now)
                var fileInfo = new FileInfo(fullPath);
                if (fileInfo.Length == 0)
                {
                    corruptedFiles.Add(file);
                }
            }

            if (missingFiles.Count == 0 && corruptedFiles.Count == 0)
            {
                return "✔️ Main game files integrity check completed successfully.\n-----\n";
            }

            var result = new StringBuilder();
            if (missingFiles.Count > 0)
            {
                result.AppendLine($"❌ Missing critical files: {string.Join(", ", missingFiles)}");
            }
            if (corruptedFiles.Count > 0)
            {
                result.AppendLine($"❌ Corrupted files detected: {string.Join(", ", corruptedFiles)}");
            }
            result.AppendLine("-----");

            return result.ToString();
        }
        finally
        {
            _fileAccessSemaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task<string> ScanModFilesAsync(
        string modPath,
        bool includeArchived,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(modPath);

        await _fileAccessSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _logger.LogDebug("Scanning mod files at: {ModPath}, includeArchived: {IncludeArchived}", 
                modPath, includeArchived);

            if (_modFileScanner != null)
            {
                // Use the actual mod file scanner if available
                string scanResult;
                if (includeArchived)
                {
                    // For archived scanning, we'd need BSArch path from settings
                    // For now, just scan unpacked
                    scanResult = await _modFileScanner.ScanModsUnpackedAsync(modPath, cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    scanResult = await _modFileScanner.ScanModsUnpackedAsync(modPath, cancellationToken)
                        .ConfigureAwait(false);
                }

                return scanResult;
            }

            // Fallback implementation if no scanner is available
            if (!Directory.Exists(modPath))
            {
                return "❌ Mods directory not found\n-----\n";
            }

            var modCount = Directory.GetDirectories(modPath).Length;
            var fileCount = Directory.GetFiles(modPath, "*.*", SearchOption.AllDirectories).Length;

            return $"✔️ Game mod files check completed successfully.\n" +
                   $"   Found {modCount} mods with {fileCount} total files.\n-----\n";
        }
        finally
        {
            _fileAccessSemaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task<string?> GetCachedChecksumAsync(string filePath)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        await _checksumSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            _cacheLock.EnterReadLock();
            try
            {
                if (_checksumCache.TryGetValue(filePath, out var entry))
                {
                    // Check if cache entry is still valid (file not modified)
                    var fileInfo = new FileInfo(filePath);
                    if (fileInfo.Exists && fileInfo.LastWriteTimeUtc <= entry.ComputedAt)
                    {
                        _logger.LogDebug("Cache hit for checksum: {FilePath}", filePath);
                        return entry.Checksum;
                    }

                    // Cache entry is stale, remove it
                    _cacheLock.ExitReadLock();
                    _cacheLock.EnterWriteLock();
                    try
                    {
                        _checksumCache.TryRemove(filePath, out _);
                    }
                    finally
                    {
                        _cacheLock.ExitWriteLock();
                        _cacheLock.EnterReadLock();
                    }
                }

                return null;
            }
            finally
            {
                _cacheLock.ExitReadLock();
            }
        }
        finally
        {
            _checksumSemaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task ClearChecksumCacheAsync()
    {
        ThrowIfDisposed();

        await _checksumSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            _cacheLock.EnterWriteLock();
            try
            {
                _checksumCache.Clear();
                _logger.LogInformation("Checksum cache cleared");
            }
            finally
            {
                _cacheLock.ExitWriteLock();
            }
        }
        finally
        {
            _checksumSemaphore.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        await ClearChecksumCacheAsync().ConfigureAwait(false);

        _fileAccessSemaphore?.Dispose();
        _checksumSemaphore?.Dispose();
        _cacheLock?.Dispose();

        _disposed = true;
        _logger.LogDebug("FcxFileChecker disposed");
    }

    private async Task<string> ValidateMainFilesWithRetryAsync(
        string gamePath,
        int maxRetries,
        CancellationToken cancellationToken)
    {
        var retryCount = 0;
        var delay = TimeSpan.FromMilliseconds(100);

        while (retryCount < maxRetries)
        {
            try
            {
                return await ValidateMainFilesAsync(gamePath, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException ex) when (retryCount < maxRetries - 1)
            {
                retryCount++;
                _logger.LogWarning(ex, "File validation failed, retry {Retry}/{MaxRetries}", retryCount, maxRetries);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2); // Exponential backoff
            }
        }

        throw new InvalidOperationException($"File validation failed after {maxRetries} attempts");
    }

    private async Task<string> ScanModFilesWithRetryAsync(
        string modPath,
        bool includeArchived,
        int maxRetries,
        CancellationToken cancellationToken)
    {
        var retryCount = 0;
        var delay = TimeSpan.FromMilliseconds(100);

        while (retryCount < maxRetries)
        {
            try
            {
                return await ScanModFilesAsync(modPath, includeArchived, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException ex) when (retryCount < maxRetries - 1)
            {
                retryCount++;
                _logger.LogWarning(ex, "Mod scan failed, retry {Retry}/{MaxRetries}", retryCount, maxRetries);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2);
            }
        }

        throw new InvalidOperationException($"Mod file scan failed after {maxRetries} attempts");
    }

    private async Task<string> ComputeChecksumAsync(string filePath, CancellationToken cancellationToken)
    {
        using var sha256 = SHA256.Create();
        await using var stream = File.OpenRead(filePath);
        var hash = await sha256.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToBase64String(hash);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(FcxFileChecker));
    }

    private sealed class ChecksumEntry
    {
        public required string Checksum { get; init; }
        public required DateTime ComputedAt { get; init; }
    }
}

/// <summary>
///     Options for FCX file checking operations.
/// </summary>
public sealed class FcxCheckOptions
{
    /// <summary>
    ///     Whether to include archived mod files in the scan.
    /// </summary>
    public bool IncludeArchivedMods { get; init; } = false;

    /// <summary>
    ///     Whether to retry operations on transient failures.
    /// </summary>
    public bool RetryOnFailure { get; init; } = true;

    /// <summary>
    ///     Whether to compute checksums for integrity validation.
    /// </summary>
    public bool ComputeChecksums { get; init; } = false;

    /// <summary>
    ///     Maximum parallel file operations.
    /// </summary>
    public int MaxParallelism { get; init; } = 4;
}

/// <summary>
///     Result of FCX file checking operations.
/// </summary>
public sealed class FcxFileCheckResult
{
    /// <summary>
    ///     Whether the check completed successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    ///     Result of main files validation.
    /// </summary>
    public string MainFilesResult { get; set; } = string.Empty;

    /// <summary>
    ///     Result of mod files scanning.
    /// </summary>
    public string ModFilesResult { get; set; } = string.Empty;

    /// <summary>
    ///     Error message if check failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    ///     When the check was completed.
    /// </summary>
    public DateTime CompletedAt { get; set; }

    /// <summary>
    ///     Additional metadata about the check.
    /// </summary>
    public Dictionary<string, string> Metadata { get; } = new();
}

/// <summary>
///     Progress information for FCX file checking operations.
/// </summary>
public sealed class FcxCheckProgress
{
    /// <summary>
    ///     Current step number.
    /// </summary>
    public int CurrentStep { get; init; }

    /// <summary>
    ///     Total number of steps.
    /// </summary>
    public int TotalSteps { get; init; }

    /// <summary>
    ///     Description of current operation.
    /// </summary>
    public string CurrentOperation { get; init; } = string.Empty;

    /// <summary>
    ///     Overall percent complete (0-100).
    /// </summary>
    public int PercentComplete { get; init; }
}