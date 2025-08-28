using System.Collections.Concurrent;
using System.IO.MemoryMappedFiles;
using System.Text;
using Microsoft.Extensions.Caching.Memory;

namespace Scanner111.Test.Infrastructure.Fixtures;

/// <summary>
/// Provides high-performance, cached access to sample data files.
/// Loads sample logs once and keeps them in memory for all tests in the collection.
/// </summary>
public sealed class SampleDataCacheFixture : IAsyncLifetime, IDisposable
{
    private readonly ConcurrentDictionary<string, string> _cachedSamples;
    private readonly ConcurrentDictionary<string, byte[]> _binaryCache;
    private readonly MemoryCache _processedCache;
    private readonly SemaphoreSlim _loadLock;
    private readonly Dictionary<string, MemoryMappedFile> _memoryMappedFiles;
    private bool _disposed;

    // Sample data paths
    private const string SampleLogsPath = "sample_logs/FO4";
    private const string SampleOutputPath = "sample_output";
    private const int MaxCachedSamples = 100;
    private const long MaxCacheSizeBytes = 100 * 1024 * 1024; // 100MB

    public SampleDataCacheFixture()
    {
        _cachedSamples = new ConcurrentDictionary<string, string>();
        _binaryCache = new ConcurrentDictionary<string, byte[]>();
        _memoryMappedFiles = new Dictionary<string, MemoryMappedFile>();
        _loadLock = new SemaphoreSlim(1, 1);
        
        _processedCache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = MaxCachedSamples,
            CompactionPercentage = 0.25
        });
    }

    /// <summary>
    /// Gets the content of a sample log file from cache or loads it.
    /// Thread-safe and optimized for concurrent access.
    /// </summary>
    public async Task<string> GetSampleContentAsync(string fileName)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SampleDataCacheFixture));

        // Try to get from cache first
        if (_cachedSamples.TryGetValue(fileName, out var cached))
            return cached;

        // Load with lock to prevent duplicate loading
        await _loadLock.WaitAsync().ConfigureAwait(false);
        try
        {
            // Double-check after acquiring lock
            if (_cachedSamples.TryGetValue(fileName, out cached))
                return cached;

            // Load the file
            var content = await LoadSampleFileAsync(fileName).ConfigureAwait(false);
            _cachedSamples.TryAdd(fileName, content);
            return content;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    /// <summary>
    /// Gets sample content as a byte array for binary operations.
    /// </summary>
    public async Task<byte[]> GetSampleBytesAsync(string fileName)
    {
        if (_binaryCache.TryGetValue(fileName, out var bytes))
            return bytes;

        var content = await GetSampleContentAsync(fileName).ConfigureAwait(false);
        bytes = Encoding.UTF8.GetBytes(content);
        _binaryCache.TryAdd(fileName, bytes);
        return bytes;
    }

    /// <summary>
    /// Gets a memory-mapped view of a large sample file.
    /// Useful for files > 1MB to avoid loading entire content into memory.
    /// </summary>
    public async Task<MemoryMappedViewStream> GetMemoryMappedStreamAsync(string fileName)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SampleDataCacheFixture));

        await _loadLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_memoryMappedFiles.ContainsKey(fileName))
            {
                var fullPath = GetSampleFilePath(fileName);
                if (!File.Exists(fullPath))
                    throw new FileNotFoundException($"Sample file not found: {fileName}");

                var fileInfo = new FileInfo(fullPath);
                var mmf = MemoryMappedFile.CreateFromFile(
                    fullPath,
                    FileMode.Open,
                    $"Scanner111_Sample_{fileName}",
                    fileInfo.Length,
                    MemoryMappedFileAccess.Read);

                _memoryMappedFiles[fileName] = mmf;
            }

            return _memoryMappedFiles[fileName].CreateViewStream(0, 0, MemoryMappedFileAccess.Read);
        }
        finally
        {
            _loadLock.Release();
        }
    }

    /// <summary>
    /// Gets all available sample log files.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetAvailableSamplesAsync()
    {
        return await Task.Run<IReadOnlyList<string>>(() =>
        {
            var sampleDir = GetSampleDirectoryPath();
            if (!Directory.Exists(sampleDir))
                return Array.Empty<string>();

            return Directory.GetFiles(sampleDir, "*.log")
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrEmpty(name))
                .Cast<string>()
                .ToList();
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets a processed/parsed version of sample data with caching.
    /// </summary>
    public async Task<T> GetProcessedSampleAsync<T>(string fileName, Func<string, Task<T>> processor)
        where T : class
    {
        var cacheKey = $"{typeof(T).Name}_{fileName}";
        
        if (_processedCache.TryGetValue<T>(cacheKey, out var processed) && processed != null)
            return processed;

        var content = await GetSampleContentAsync(fileName).ConfigureAwait(false);
        processed = await processor(content).ConfigureAwait(false);
        
        var cacheOptions = new MemoryCacheEntryOptions
        {
            Size = 1,
            SlidingExpiration = TimeSpan.FromMinutes(10)
        };
        
        _processedCache.Set(cacheKey, processed, cacheOptions);
        return processed;
    }

    /// <summary>
    /// Preloads commonly used samples for better performance.
    /// </summary>
    public async Task PreloadCommonSamplesAsync()
    {
        var commonSamples = new[]
        {
            "crash-2022-06-05-12-52-17.log",
            "crash-2022-06-09-07-25-03.log",
            "crash-2022-06-12-07-11-38.log",
            "crash-2022-06-15-10-02-51.log"
        };

        var loadTasks = commonSamples.Select(GetSampleContentAsync);
        await Task.WhenAll(loadTasks).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets matching pairs of sample logs and their expected outputs.
    /// </summary>
    public async Task<IReadOnlyList<(string LogFile, string OutputFile, string LogContent, string OutputContent)>> 
        GetMatchingPairsAsync()
    {
        var pairs = new List<(string, string, string, string)>();
        
        var logFiles = await GetAvailableSamplesAsync().ConfigureAwait(false);
        foreach (var logFile in logFiles)
        {
            var baseName = Path.GetFileNameWithoutExtension(logFile);
            var outputFile = $"{baseName}.txt";
            var outputPath = Path.Combine(SampleOutputPath, outputFile);
            
            if (File.Exists(outputPath))
            {
                var logContent = await GetSampleContentAsync(logFile).ConfigureAwait(false);
                var outputContent = await File.ReadAllTextAsync(outputPath).ConfigureAwait(false);
                pairs.Add((logFile, outputFile, logContent, outputContent));
            }
        }

        return pairs;
    }

    public async Task InitializeAsync()
    {
        // Optionally preload common samples
        if (Directory.Exists(GetSampleDirectoryPath()))
        {
            await PreloadCommonSamplesAsync().ConfigureAwait(false);
        }
    }

    public Task DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed) return;

        // Dispose memory-mapped files
        foreach (var mmf in _memoryMappedFiles.Values)
        {
            try
            {
                mmf?.Dispose();
            }
            catch
            {
                // Best effort cleanup
            }
        }
        _memoryMappedFiles.Clear();

        // Clear caches
        _cachedSamples.Clear();
        _binaryCache.Clear();
        _processedCache?.Dispose();
        _loadLock?.Dispose();
        
        _disposed = true;
    }

    private async Task<string> LoadSampleFileAsync(string fileName)
    {
        var fullPath = GetSampleFilePath(fileName);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Sample file not found: {fileName}");

        var fileInfo = new FileInfo(fullPath);
        
        // Use memory-mapped files for large files (> 1MB)
        if (fileInfo.Length > 1024 * 1024)
        {
            return await LoadLargeFileAsync(fullPath).ConfigureAwait(false);
        }

        // Regular read for smaller files
        return await File.ReadAllTextAsync(fullPath).ConfigureAwait(false);
    }

    private async Task<string> LoadLargeFileAsync(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 
            bufferSize: 4096, useAsync: true);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync().ConfigureAwait(false);
    }

    private static string GetSampleFilePath(string fileName)
    {
        // Try multiple possible locations
        var possiblePaths = new[]
        {
            Path.Combine(SampleLogsPath, fileName),
            Path.Combine("sample_logs", fileName),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SampleLogsPath, fileName),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sample_logs", fileName)
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
                return path;
        }

        // Default to first path if none exist (will throw FileNotFoundException)
        return possiblePaths[0];
    }

    private static string GetSampleDirectoryPath()
    {
        var possiblePaths = new[]
        {
            SampleLogsPath,
            "sample_logs",
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SampleLogsPath),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sample_logs", "FO4")
        };

        foreach (var path in possiblePaths)
        {
            if (Directory.Exists(path))
                return path;
        }

        return possiblePaths[0];
    }
}