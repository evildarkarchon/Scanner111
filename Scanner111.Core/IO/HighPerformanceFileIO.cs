using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Scanner111.Core.IO;

/// <summary>
///     High-performance file I/O service combining multiple optimization techniques.
///     Uses memory-mapped files, pipelines, and buffer pooling for maximum efficiency.
/// </summary>
public sealed class HighPerformanceFileIO : IFileIoCore, IAsyncDisposable
{
    private readonly ILogger<HighPerformanceFileIO> _logger;
    private readonly MemoryMappedFileHandler _memoryMappedHandler;
    private readonly ArrayPool<byte> _bufferPool;
    private readonly ArrayPool<char> _charPool;
    private readonly Encoding _defaultEncoding;
    private readonly SemaphoreSlim _concurrencyLimiter;
    private bool _disposed;

    public HighPerformanceFileIO(
        ILogger<HighPerformanceFileIO> logger,
        MemoryMappedFileHandler memoryMappedHandler)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _memoryMappedHandler = memoryMappedHandler ?? throw new ArgumentNullException(nameof(memoryMappedHandler));
        _bufferPool = ArrayPool<byte>.Shared;
        _charPool = ArrayPool<char>.Shared;
        _defaultEncoding = Encoding.UTF8;
        _concurrencyLimiter = new SemaphoreSlim(Environment.ProcessorCount * 2);
        
        // Register code pages for legacy encoding support
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <inheritdoc />
    public async Task<string> ReadFileAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        
        var fileInfo = new FileInfo(path);
        
        // Use memory-mapped files for large files
        if (fileInfo.Length > 1024 * 1024) // > 1MB
        {
            return await ReadLargeFileAsync(path, cancellationToken);
        }
        
        // Use standard async for small files
        return await ReadSmallFileAsync(path, cancellationToken);
    }

    private async Task<string> ReadLargeFileAsync(string path, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Reading large file using memory mapping: {Path}", path);
        
        var stopwatch = Stopwatch.StartNew();
        var lines = new List<string>();

        await foreach (var line in _memoryMappedHandler.ReadLinesAsync(path, _defaultEncoding, cancellationToken))
        {
            lines.Add(line);
        }

        stopwatch.Stop();
        var result = string.Join(Environment.NewLine, lines);
        
        _logger.LogInformation("Large file read completed in {Time:F2}s ({Size:N0} bytes, {Rate:F2} MB/s)",
            stopwatch.Elapsed.TotalSeconds,
            result.Length,
            result.Length / 1024.0 / 1024.0 / stopwatch.Elapsed.TotalSeconds);

        return result;
    }

    private async Task<string> ReadSmallFileAsync(string path, CancellationToken cancellationToken)
    {
        var encoding = await DetectEncodingAsync(path, cancellationToken);
        
        // Use buffer pooling for efficiency
        var buffer = _bufferPool.Rent(4096);
        try
        {
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true);

            using var reader = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: true);
            return await reader.ReadToEndAsync(cancellationToken);
        }
        finally
        {
            _bufferPool.Return(buffer);
        }
    }

    /// <inheritdoc />
    public async Task<string[]> ReadLinesAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fileInfo = new FileInfo(path);
        
        // Use memory-mapped files for large files
        if (fileInfo.Length > 1024 * 1024) // > 1MB
        {
            var lines = new List<string>();
            await foreach (var line in _memoryMappedHandler.ReadLinesAsync(path, _defaultEncoding, cancellationToken))
            {
                lines.Add(line);
            }
            return lines.ToArray();
        }

        // Use standard async for small files
        var encoding = await DetectEncodingAsync(path, cancellationToken);
        return await File.ReadAllLinesAsync(path, encoding, cancellationToken);
    }

    /// <inheritdoc />
    public async Task WriteFileAsync(
        string path,
        string content,
        Encoding? encoding = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(content);

        encoding ??= _defaultEncoding;

        // Ensure directory exists
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Use atomic write for safety
        await WriteFileAtomicAsync(path, content, encoding, cancellationToken);
    }

    private async Task WriteFileAtomicAsync(
        string path,
        string content,
        Encoding encoding,
        CancellationToken cancellationToken)
    {
        var tempPath = $"{path}.tmp{Guid.NewGuid():N}";
        
        try
        {
            // Write to temporary file first
            await using (var stream = new FileStream(
                tempPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 65536, // 64KB buffer
                useAsync: true))
            {
                await using var writer = new StreamWriter(stream, encoding);
                await writer.WriteAsync(content.AsMemory(), cancellationToken);
                await writer.FlushAsync(cancellationToken);
            }

            // Atomic rename
            File.Move(tempPath, path, overwrite: true);
            
            _logger.LogDebug("Atomic write completed for {Path}", path);
        }
        catch
        {
            // Clean up temp file on failure
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* Best effort */ }
            }
            throw;
        }
    }

    /// <inheritdoc />
    public async Task WriteLinesAsync(
        string path,
        IEnumerable<string> lines,
        Encoding? encoding = null,
        CancellationToken cancellationToken = default)
    {
        var content = string.Join(Environment.NewLine, lines);
        await WriteFileAsync(path, content, encoding, cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(File.Exists(path));
    }

    /// <inheritdoc />
    public Task<bool> DirectoryExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Directory.Exists(path));
    }

    /// <inheritdoc />
    public Task<DateTime?> GetLastWriteTimeAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
            return Task.FromResult<DateTime?>(null);
            
        return Task.FromResult<DateTime?>(File.GetLastWriteTimeUtc(path));
    }

    /// <inheritdoc />
    public Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
            _logger.LogDebug("Created directory: {Path}", path);
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteFileAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
            return false;

        await Task.Run(() => File.Delete(path), cancellationToken);
        _logger.LogDebug("Deleted file: {Path}", path);
        return true;
    }

    /// <inheritdoc />
    public async Task CopyFileAsync(
        string sourcePath,
        string destinationPath,
        bool overwrite = false,
        CancellationToken cancellationToken = default)
    {
        // Use async streams for efficient copying
        const int bufferSize = 81920; // 80KB
        
        await using var sourceStream = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize,
            useAsync: true);
            
        await using var destStream = new FileStream(
            destinationPath,
            overwrite ? FileMode.Create : FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize,
            useAsync: true);
            
        await sourceStream.CopyToAsync(destStream, bufferSize, cancellationToken);
        
        _logger.LogDebug("Copied file from {Source} to {Dest}", sourcePath, destinationPath);
    }

    /// <inheritdoc />
    public async Task<Encoding> DetectEncodingAsync(string path, CancellationToken cancellationToken = default)
    {
        const int sampleSize = 4096;
        var buffer = _bufferPool.Rent(sampleSize);
        
        try
        {
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: sampleSize,
                useAsync: true);

            var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, sampleSize), cancellationToken);
            
            // Check for BOM
            if (bytesRead >= 3)
            {
                // UTF-8 BOM
                if (buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
                    return Encoding.UTF8;
                    
                // UTF-16 LE BOM
                if (buffer[0] == 0xFF && buffer[1] == 0xFE)
                    return Encoding.Unicode;
                    
                // UTF-16 BE BOM
                if (buffer[0] == 0xFE && buffer[1] == 0xFF)
                    return Encoding.BigEndianUnicode;
            }

            // Try to detect UTF-8
            if (IsValidUtf8(buffer.AsSpan(0, bytesRead)))
                return Encoding.UTF8;

            // Fall back to default
            return _defaultEncoding;
        }
        finally
        {
            _bufferPool.Return(buffer);
        }
    }

    /// <summary>
    ///     Processes multiple files in parallel with optimal resource usage.
    /// </summary>
    public async Task<Dictionary<string, T>> ProcessFilesInParallelAsync<T>(
        IEnumerable<string> filePaths,
        Func<string, string, Task<T>> processor,
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, T>();
        var tasks = new List<Task<(string Path, T Result)>>();

        foreach (var path in filePaths)
        {
            tasks.Add(ProcessFileWithConcurrencyLimitAsync(path, processor, cancellationToken));
        }

        var completed = await Task.WhenAll(tasks);
        
        foreach (var (path, result) in completed)
        {
            results[path] = result;
        }

        return results;
    }

    private async Task<(string Path, T Result)> ProcessFileWithConcurrencyLimitAsync<T>(
        string path,
        Func<string, string, Task<T>> processor,
        CancellationToken cancellationToken)
    {
        await _concurrencyLimiter.WaitAsync(cancellationToken);
        try
        {
            var content = await ReadFileAsync(path, cancellationToken);
            var result = await processor(path, content);
            return (path, result);
        }
        finally
        {
            _concurrencyLimiter.Release();
        }
    }

    /// <summary>
    ///     Reads a file using System.IO.Pipelines for maximum efficiency.
    /// </summary>
    public async Task<ReadResult> ReadWithPipelineAsync(
        string path,
        Func<ReadOnlySequence<byte>, Task> processor,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        long bytesProcessed = 0;

        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 0, // Let PipeReader handle buffering
            useAsync: true);

        var reader = PipeReader.Create(stream);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var result = await reader.ReadAsync(cancellationToken);
                var buffer = result.Buffer;

                if (buffer.Length > 0)
                {
                    await processor(buffer);
                    bytesProcessed += buffer.Length;
                }

                reader.AdvanceTo(buffer.End);

                if (result.IsCompleted)
                    break;
            }
        }
        finally
        {
            await reader.CompleteAsync();
        }

        stopwatch.Stop();
        
        return new ReadResult
        {
            BytesProcessed = bytesProcessed,
            ProcessingTime = stopwatch.Elapsed,
            ThroughputMBps = bytesProcessed / 1024.0 / 1024.0 / stopwatch.Elapsed.TotalSeconds
        };
    }

    private static bool IsValidUtf8(ReadOnlySpan<byte> data)
    {
        try
        {
            _ = Encoding.UTF8.GetCharCount(data);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _concurrencyLimiter?.Dispose();
        await _memoryMappedHandler.DisposeAsync();
        
        _disposed = true;
    }
}

/// <summary>
///     Result of pipeline-based file reading.
/// </summary>
public sealed class ReadResult
{
    public long BytesProcessed { get; init; }
    public TimeSpan ProcessingTime { get; init; }
    public double ThroughputMBps { get; init; }
}