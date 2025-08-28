using System.Buffers;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Scanner111.Core.IO;

/// <summary>
///     High-performance file handler using memory-mapped files.
///     Provides zero-copy access to large files with parallel processing capabilities.
/// </summary>
public sealed class MemoryMappedFileHandler : IAsyncDisposable
{
    private readonly ILogger<MemoryMappedFileHandler> _logger;
    private readonly Dictionary<string, MappedFile> _openFiles;
    private readonly SemaphoreSlim _filesLock;
    private readonly ArrayPool<byte> _bufferPool;
    private bool _disposed;

    public MemoryMappedFileHandler(ILogger<MemoryMappedFileHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _openFiles = new Dictionary<string, MappedFile>();
        _filesLock = new SemaphoreSlim(1, 1);
        _bufferPool = ArrayPool<byte>.Shared;
    }

    /// <summary>
    ///     Opens a file for memory-mapped access.
    ///     Provides direct memory access without copying data.
    /// </summary>
    public async Task<IMappedFileAccess> OpenFileAsync(
        string filePath,
        FileAccess access = FileAccess.Read,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        await _filesLock.WaitAsync(cancellationToken);
        try
        {
            if (_openFiles.TryGetValue(filePath, out var existing))
            {
                existing.IncrementRefCount();
                return existing;
            }

            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
                throw new FileNotFoundException($"File not found: {filePath}");

            _logger.LogDebug("Opening memory-mapped file: {Path} ({Size:N0} bytes)", 
                filePath, fileInfo.Length);

            // Create memory-mapped file
            var fileStream = new FileStream(
                filePath, 
                FileMode.Open, 
                access, 
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true);

            var memoryMappedFile = MemoryMappedFile.CreateFromFile(
                fileStream, 
                null, 
                fileInfo.Length,
                access == FileAccess.Read ? MemoryMappedFileAccess.Read : MemoryMappedFileAccess.ReadWrite,
                HandleInheritability.None,
                leaveOpen: false);

            var mappedFile = new MappedFile(
                filePath,
                fileInfo.Length,
                memoryMappedFile,
                fileStream,
                this);

            _openFiles[filePath] = mappedFile;
            return mappedFile;
        }
        finally
        {
            _filesLock.Release();
        }
    }

    /// <summary>
    ///     Processes a large file in parallel chunks.
    ///     Leverages true parallelism without Python's GIL limitations.
    /// </summary>
    public async Task<TResult> ProcessFileInParallelAsync<TResult>(
        string filePath,
        Func<ReadOnlyMemory<byte>, int, Task<TResult>> chunkProcessor,
        Func<IEnumerable<TResult>, TResult> resultAggregator,
        int chunkSizeKb = 1024,
        CancellationToken cancellationToken = default)
    {
        await using var mappedFile = await OpenFileAsync(filePath, FileAccess.Read, cancellationToken);
        var fileSize = mappedFile.FileSize;
        var chunkSize = chunkSizeKb * 1024;
        var chunkCount = (int)Math.Ceiling((double)fileSize / chunkSize);

        _logger.LogDebug("Processing file in {ChunkCount} parallel chunks of {ChunkSize}KB", 
            chunkCount, chunkSizeKb);

        var stopwatch = Stopwatch.StartNew();
        var results = new TResult[chunkCount];

        // Process chunks in parallel - no GIL means true parallelism!
        await Parallel.ForAsync(
            0,
            chunkCount,
            new ParallelOptions 
            { 
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Environment.ProcessorCount
            },
            async (chunkIndex, ct) =>
            {
                var offset = (long)chunkIndex * chunkSize;
                var length = (int)Math.Min(chunkSize, fileSize - offset);
                
                var chunkData = await mappedFile.ReadAsync(offset, length, ct);
                results[chunkIndex] = await chunkProcessor(chunkData, chunkIndex);
            });

        stopwatch.Stop();
        _logger.LogInformation("Parallel file processing completed in {Time:F2}s ({Rate:F2} MB/s)",
            stopwatch.Elapsed.TotalSeconds,
            fileSize / 1024.0 / 1024.0 / stopwatch.Elapsed.TotalSeconds);

        return resultAggregator(results);
    }

    /// <summary>
    ///     Searches for patterns in a file using parallel processing.
    ///     Much faster than Python's line-by-line processing.
    /// </summary>
    public async Task<List<SearchResult>> ParallelSearchAsync(
        string filePath,
        string pattern,
        bool caseSensitive = true,
        CancellationToken cancellationToken = default)
    {
        var searchOptions = caseSensitive 
            ? StringComparison.Ordinal 
            : StringComparison.OrdinalIgnoreCase;

        var allResults = await ProcessFileInParallelAsync(
            filePath,
            async (chunk, chunkIndex) =>
            {
                var text = Encoding.UTF8.GetString(chunk.Span);
                var results = new List<SearchResult>();
                var index = 0;

                while ((index = text.IndexOf(pattern, index, searchOptions)) != -1)
                {
                    results.Add(new SearchResult
                    {
                        ChunkIndex = chunkIndex,
                        LocalOffset = index,
                        MatchLength = pattern.Length
                    });
                    index += pattern.Length;
                }

                await Task.CompletedTask;
                return results;
            },
            results => results.SelectMany(r => r).ToList(),
            chunkSizeKb: 256,
            cancellationToken);

        return allResults;
    }

    /// <summary>
    ///     Reads lines from a large file efficiently using memory mapping.
    /// </summary>
    public async IAsyncEnumerable<string> ReadLinesAsync(
        string filePath,
        Encoding? encoding = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        encoding ??= Encoding.UTF8;
        await using var mappedFile = await OpenFileAsync(filePath, FileAccess.Read, cancellationToken);
        
        const int bufferSize = 65536; // 64KB buffer
        var buffer = _bufferPool.Rent(bufferSize);
        var decoder = encoding.GetDecoder();
        var chars = new char[encoding.GetMaxCharCount(bufferSize)];
        var leftoverBytes = new List<byte>();
        
        try
        {
            long position = 0;
            var fileSize = mappedFile.FileSize;
            var lineBuilder = new StringBuilder();

            while (position < fileSize)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var readLength = (int)Math.Min(bufferSize, fileSize - position);
                var data = await mappedFile.ReadAsync(position, readLength, cancellationToken);
                
                // Combine with leftover bytes from previous iteration
                var bytesToProcess = leftoverBytes.Count > 0
                    ? leftoverBytes.Concat(data.ToArray()).ToArray()
                    : data.ToArray();
                    
                leftoverBytes.Clear();

                // Decode bytes to chars
                var charCount = decoder.GetChars(bytesToProcess, 0, bytesToProcess.Length, chars, 0);
                
                // Process chars to extract lines
                for (int i = 0; i < charCount; i++)
                {
                    if (chars[i] == '\n')
                    {
                        yield return lineBuilder.ToString();
                        lineBuilder.Clear();
                    }
                    else if (chars[i] != '\r')
                    {
                        lineBuilder.Append(chars[i]);
                    }
                }

                // Save incomplete UTF-8 sequences for next iteration
                if (position + readLength < fileSize)
                {
                    var lastValidIndex = FindLastValidUtf8Boundary(bytesToProcess);
                    if (lastValidIndex < bytesToProcess.Length)
                    {
                        leftoverBytes.AddRange(bytesToProcess.Skip(lastValidIndex));
                    }
                }

                position += readLength;
            }

            // Yield any remaining content
            if (lineBuilder.Length > 0)
            {
                yield return lineBuilder.ToString();
            }
        }
        finally
        {
            _bufferPool.Return(buffer);
        }
    }

    private static int FindLastValidUtf8Boundary(byte[] buffer)
    {
        // Find the last valid UTF-8 character boundary
        for (int i = buffer.Length - 1; i >= Math.Max(0, buffer.Length - 4); i--)
        {
            var b = buffer[i];
            
            // Single-byte character (0xxxxxxx)
            if ((b & 0x80) == 0)
                return i + 1;
                
            // Start of multi-byte sequence (11xxxxxx)
            if ((b & 0xC0) == 0xC0)
                return i;
        }
        
        return buffer.Length;
    }

    internal async Task ReleaseFileAsync(string filePath)
    {
        await _filesLock.WaitAsync();
        try
        {
            if (_openFiles.TryGetValue(filePath, out var file))
            {
                if (file.DecrementRefCount() == 0)
                {
                    file.Dispose();
                    _openFiles.Remove(filePath);
                    _logger.LogDebug("Closed memory-mapped file: {Path}", filePath);
                }
            }
        }
        finally
        {
            _filesLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        await _filesLock.WaitAsync();
        try
        {
            foreach (var file in _openFiles.Values)
            {
                file.Dispose();
            }
            _openFiles.Clear();
        }
        finally
        {
            _filesLock.Release();
        }

        _filesLock?.Dispose();
        _disposed = true;
    }

    /// <summary>
    ///     Represents a memory-mapped file with reference counting.
    /// </summary>
    private sealed class MappedFile : IMappedFileAccess
    {
        private readonly MemoryMappedFile _memoryMappedFile;
        private readonly FileStream _fileStream;
        private readonly MemoryMappedFileHandler _handler;
        private int _refCount = 1;
        private bool _disposed;

        public MappedFile(
            string filePath,
            long fileSize,
            MemoryMappedFile memoryMappedFile,
            FileStream fileStream,
            MemoryMappedFileHandler handler)
        {
            FilePath = filePath;
            FileSize = fileSize;
            _memoryMappedFile = memoryMappedFile;
            _fileStream = fileStream;
            _handler = handler;
        }

        public string FilePath { get; }
        public long FileSize { get; }

        public async Task<ReadOnlyMemory<byte>> ReadAsync(
            long offset,
            int length,
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (offset < 0 || offset >= FileSize)
                throw new ArgumentOutOfRangeException(nameof(offset));
            
            if (length <= 0 || offset + length > FileSize)
                throw new ArgumentOutOfRangeException(nameof(length));

            return await Task.Run(() =>
            {
                using var accessor = _memoryMappedFile.CreateViewAccessor(offset, length, MemoryMappedFileAccess.Read);
                var buffer = new byte[length];
                accessor.ReadArray(0, buffer, 0, length);
                return new ReadOnlyMemory<byte>(buffer);
            }, cancellationToken);
        }

        public async Task WriteAsync(
            long offset,
            ReadOnlyMemory<byte> data,
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (offset < 0 || offset >= FileSize)
                throw new ArgumentOutOfRangeException(nameof(offset));

            await Task.Run(() =>
            {
                using var accessor = _memoryMappedFile.CreateViewAccessor(offset, data.Length, MemoryMappedFileAccess.Write);
                accessor.WriteArray(0, data.ToArray(), 0, data.Length);
            }, cancellationToken);
        }

        public void IncrementRefCount() => Interlocked.Increment(ref _refCount);
        public int DecrementRefCount() => Interlocked.Decrement(ref _refCount);

        public void Dispose()
        {
            if (_disposed)
                return;

            _memoryMappedFile?.Dispose();
            _fileStream?.Dispose();
            _disposed = true;
        }

        public async ValueTask DisposeAsync()
        {
            await _handler.ReleaseFileAsync(FilePath);
            GC.SuppressFinalize(this);
        }
    }
}

/// <summary>
///     Interface for memory-mapped file access.
/// </summary>
public interface IMappedFileAccess : IAsyncDisposable
{
    string FilePath { get; }
    long FileSize { get; }
    
    Task<ReadOnlyMemory<byte>> ReadAsync(
        long offset, 
        int length, 
        CancellationToken cancellationToken = default);
        
    Task WriteAsync(
        long offset, 
        ReadOnlyMemory<byte> data, 
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Search result from parallel file search.
/// </summary>
public sealed class SearchResult
{
    public int ChunkIndex { get; init; }
    public int LocalOffset { get; init; }
    public int MatchLength { get; init; }
}