using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Scanner111.Core.IO;

namespace Scanner111.Test.IO;

public sealed class MemoryMappedFileHandlerTests : IDisposable
{
    private readonly ILogger<MemoryMappedFileHandler> _logger;
    private readonly MemoryMappedFileHandler _sut;
    private readonly string _testDirectory;
    private readonly List<string> _testFiles;

    public MemoryMappedFileHandlerTests()
    {
        _logger = Substitute.For<ILogger<MemoryMappedFileHandler>>();
        _sut = new MemoryMappedFileHandler(_logger);

        _testDirectory = Path.Combine(Path.GetTempPath(), $"MMFHandler_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        _testFiles = new List<string>();
    }

    private string CreateTestFile(string content, string? fileName = null)
    {
        fileName ??= $"test_{Guid.NewGuid():N}.txt";
        var path = Path.Combine(_testDirectory, fileName);
        File.WriteAllText(path, content);
        _testFiles.Add(path);
        return path;
    }

    private string CreateLargeTestFile(int sizeInKb, string? fileName = null)
    {
        fileName ??= $"large_{Guid.NewGuid():N}.txt";
        var path = Path.Combine(_testDirectory, fileName);
        
        var sb = new StringBuilder();
        var line = new string('X', 100); // 100 chars per line
        for (int i = 0; i < sizeInKb * 10; i++) // ~100 bytes per line * 10 = 1KB
        {
            sb.AppendLine($"Line {i:D6}: {line}");
        }
        
        File.WriteAllText(path, sb.ToString());
        _testFiles.Add(path);
        return path;
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new MemoryMappedFileHandler(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public async Task OpenFileAsync_ValidFile_OpensSuccessfully()
    {
        // Arrange
        var content = "Test content for memory mapping";
        var path = CreateTestFile(content);

        // Act
        await using var mappedFile = await _sut.OpenFileAsync(path);

        // Assert
        mappedFile.Should().NotBeNull();
        mappedFile.FilePath.Should().Be(path);
        mappedFile.FileSize.Should().Be(content.Length);
    }

    [Fact]
    public async Task OpenFileAsync_NonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var path = Path.Combine(_testDirectory, "nonexistent.txt");

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() => _sut.OpenFileAsync(path));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task OpenFileAsync_InvalidPath_ThrowsArgumentException(string? path)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.OpenFileAsync(path!));
    }

    [Fact]
    public async Task OpenFileAsync_SameFileMultipleTimes_SharesInstance()
    {
        // Arrange
        var path = CreateTestFile("Shared content");

        // Act
        await using var file1 = await _sut.OpenFileAsync(path);
        await using var file2 = await _sut.OpenFileAsync(path);
        await using var file3 = await _sut.OpenFileAsync(path);

        // Assert
        file1.Should().BeSameAs(file2);
        file2.Should().BeSameAs(file3);
    }

    [Fact]
    public async Task OpenFileAsync_WithWriteAccess_OpensForWrite()
    {
        // Arrange
        var content = "Original content";
        var path = CreateTestFile(content);

        // Act
        await using var mappedFile = await _sut.OpenFileAsync(path, FileAccess.ReadWrite);

        // Assert
        mappedFile.Should().NotBeNull();
        
        // Test write capability
        var newData = Encoding.UTF8.GetBytes("New");
        await mappedFile.WriteAsync(0, newData);
        
        var readData = await mappedFile.ReadAsync(0, newData.Length);
        Encoding.UTF8.GetString(readData.Span).Should().Be("New");
    }

    [Fact]
    public async Task OpenFileAsync_Cancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        var path = CreateTestFile("content");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => 
            _sut.OpenFileAsync(path, FileAccess.Read, cts.Token));
    }

    [Fact]
    public async Task OpenFileAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var path = CreateTestFile("content");
        await _sut.DisposeAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() => _sut.OpenFileAsync(path));
    }

    [Fact]
    public async Task ReadAsync_ValidRange_ReadsCorrectData()
    {
        // Arrange
        var content = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var path = CreateTestFile(content);

        await using var mappedFile = await _sut.OpenFileAsync(path);

        // Act
        var data = await mappedFile.ReadAsync(10, 10); // Read "ABCDEFGHIJ"

        // Assert
        var result = Encoding.UTF8.GetString(data.Span);
        result.Should().Be("ABCDEFGHIJ");
    }

    [Fact]
    public async Task ReadAsync_EntireFile_ReadsAllContent()
    {
        // Arrange
        var content = "Complete file content";
        var path = CreateTestFile(content);

        await using var mappedFile = await _sut.OpenFileAsync(path);

        // Act
        var data = await mappedFile.ReadAsync(0, (int)mappedFile.FileSize);

        // Assert
        var result = Encoding.UTF8.GetString(data.Span);
        result.Should().Be(content);
    }

    [Theory]
    [InlineData(-1, 10)]  // Negative offset
    [InlineData(1000, 10)] // Offset beyond file
    public async Task ReadAsync_InvalidOffset_ThrowsArgumentOutOfRangeException(long offset, int length)
    {
        // Arrange
        var path = CreateTestFile("Short content");
        await using var mappedFile = await _sut.OpenFileAsync(path);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => 
            mappedFile.ReadAsync(offset, length));
    }

    [Theory]
    [InlineData(0, -1)]   // Negative length
    [InlineData(0, 0)]    // Zero length
    [InlineData(5, 100)]  // Length exceeds file size
    public async Task ReadAsync_InvalidLength_ThrowsArgumentOutOfRangeException(long offset, int length)
    {
        // Arrange
        var path = CreateTestFile("Short content");
        await using var mappedFile = await _sut.OpenFileAsync(path);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => 
            mappedFile.ReadAsync(offset, length));
    }

    [Fact]
    public async Task WriteAsync_ValidData_WritesCorrectly()
    {
        // Arrange
        var original = "Original content here";
        var path = CreateTestFile(original);
        
        await using var mappedFile = await _sut.OpenFileAsync(path, FileAccess.ReadWrite);
        
        // Act
        var newData = Encoding.UTF8.GetBytes("MODIFIED");
        await mappedFile.WriteAsync(9, newData); // Replace "content" with "MODIFIED"

        // Assert
        var result = await File.ReadAllTextAsync(path);
        result.Should().StartWith("Original MODIFIED");
    }

    [Theory]
    [InlineData(-1)]   // Negative offset
    [InlineData(1000)] // Offset beyond file
    public async Task WriteAsync_InvalidOffset_ThrowsArgumentOutOfRangeException(long offset)
    {
        // Arrange
        var path = CreateTestFile("Short content");
        await using var mappedFile = await _sut.OpenFileAsync(path, FileAccess.ReadWrite);
        var data = Encoding.UTF8.GetBytes("data");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => 
            mappedFile.WriteAsync(offset, data));
    }

    [Fact]
    public async Task ProcessFileInParallelAsync_ProcessesChunksCorrectly()
    {
        // Arrange
        var path = CreateLargeTestFile(10); // 10KB file
        var processedChunks = new List<int>();

        Task<int> ChunkProcessor(ReadOnlyMemory<byte> chunk, int index)
        {
            lock (processedChunks)
            {
                processedChunks.Add(index);
            }
            return Task.FromResult(chunk.Length);
        }

        int Aggregator(IEnumerable<int> results) => results.Sum();

        // Act
        var totalBytes = await _sut.ProcessFileInParallelAsync(
            path, 
            ChunkProcessor, 
            Aggregator,
            chunkSizeKb: 2); // 2KB chunks

        // Assert
        totalBytes.Should().BeGreaterThan(0);
        processedChunks.Should().NotBeEmpty();
        processedChunks.Distinct().Should().HaveCountGreaterThanOrEqualTo(5); // At least 5 chunks for 10KB file
    }

    [Fact]
    public async Task ProcessFileInParallelAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var path = CreateLargeTestFile(100); // Large file for longer processing
        var cts = new CancellationTokenSource();
        
        async Task<int> SlowProcessor(ReadOnlyMemory<byte> chunk, int index)
        {
            await Task.Delay(100); // Simulate slow processing
            return chunk.Length;
        }

        cts.CancelAfter(50);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _sut.ProcessFileInParallelAsync(
                path,
                SlowProcessor,
                results => results.Sum(),
                chunkSizeKb: 1,
                cts.Token));
    }

    [Fact]
    public async Task ParallelSearchAsync_FindsAllMatches()
    {
        // Arrange
        var content = @"
            This is a test file.
            We are searching for TEST patterns.
            Another TEST appears here.
            And one more test at the end.
        ";
        var path = CreateTestFile(content);

        // Act
        var results = await _sut.ParallelSearchAsync(path, "test", caseSensitive: false);

        // Assert
        results.Should().HaveCount(4);
        results.Should().AllSatisfy(r =>
        {
            r.MatchLength.Should().Be(4);
            r.ChunkIndex.Should().BeGreaterOrEqualTo(0);
        });
    }

    [Fact]
    public async Task ParallelSearchAsync_CaseSensitive_FindsOnlyCaseSensitiveMatches()
    {
        // Arrange
        var content = "Test TEST test TeSt";
        var path = CreateTestFile(content);

        // Act
        var results = await _sut.ParallelSearchAsync(path, "test", caseSensitive: true);

        // Assert
        results.Should().HaveCount(1); // Only lowercase "test"
    }

    [Fact]
    public async Task ParallelSearchAsync_NoMatches_ReturnsEmpty()
    {
        // Arrange
        var content = "This file contains no matching patterns.";
        var path = CreateTestFile(content);

        // Act
        var results = await _sut.ParallelSearchAsync(path, "NOMATCH");

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadLinesAsync_ReadsAllLines()
    {
        // Arrange
        var lines = new[] { "Line 1", "Line 2", "Line 3", "Line 4" };
        var content = string.Join(Environment.NewLine, lines);
        var path = CreateTestFile(content);

        // Act
        var result = new List<string>();
        await foreach (var line in _sut.ReadLinesAsync(path))
        {
            result.Add(line);
        }

        // Assert
        result.Should().BeEquivalentTo(lines);
    }

    [Fact]
    public async Task ReadLinesAsync_EmptyFile_YieldsNothing()
    {
        // Arrange
        var path = CreateTestFile("");

        // Act
        var result = new List<string>();
        await foreach (var line in _sut.ReadLinesAsync(path))
        {
            result.Add(line);
        }

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadLinesAsync_LargeFile_StreamsEfficiently()
    {
        // Arrange
        var path = CreateLargeTestFile(100); // 100KB file
        var lineCount = 0;

        // Act
        await foreach (var line in _sut.ReadLinesAsync(path))
        {
            lineCount++;
            if (lineCount > 10000) break; // Safety limit
        }

        // Assert
        lineCount.Should().BeGreaterThan(100);
    }

    [Fact]
    public async Task ReadLinesAsync_WithDifferentLineEndings_HandlesCorrectly()
    {
        // Arrange
        var content = "Line1\nLine2\r\nLine3\rLine4";
        var path = CreateTestFile(content);

        // Act
        var lines = new List<string>();
        await foreach (var line in _sut.ReadLinesAsync(path))
        {
            lines.Add(line);
        }

        // Assert
        lines.Should().HaveCount(4);
        lines.Should().BeEquivalentTo(new[] { "Line1", "Line2", "Line3", "Line4" });
    }

    [Fact]
    public async Task ReadLinesAsync_WithUtf8Encoding_ReadsCorrectly()
    {
        // Arrange
        var lines = new[] { "UTF-8: ñ, é, ü", "Special: 日本語", "Emoji: 😀" };
        var content = string.Join("\n", lines);
        var path = CreateTestFile(content);

        // Act
        var result = new List<string>();
        await foreach (var line in _sut.ReadLinesAsync(path, Encoding.UTF8))
        {
            result.Add(line);
        }

        // Assert
        result.Should().BeEquivalentTo(lines);
    }

    [Fact]
    public async Task ReadLinesAsync_Cancelled_StopsIteration()
    {
        // Arrange
        var path = CreateLargeTestFile(100);
        var cts = new CancellationTokenSource();
        var lineCount = 0;

        // Act
        cts.CancelAfter(50);
        
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var line in _sut.ReadLinesAsync(path, cancellationToken: cts.Token))
            {
                lineCount++;
                await Task.Delay(10); // Slow down to ensure cancellation happens
            }
        });

        // Assert
        lineCount.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task ReleaseFileAsync_DecreasesRefCount()
    {
        // Arrange
        var path = CreateTestFile("content");
        
        // Open file multiple times
        var file1 = await _sut.OpenFileAsync(path);
        var file2 = await _sut.OpenFileAsync(path);
        var file3 = await _sut.OpenFileAsync(path);

        // Act - Release references
        await file1.DisposeAsync();
        await file2.DisposeAsync();
        
        // File should still be accessible
        var data = await file3.ReadAsync(0, 5);
        data.Should().NotBeNull();

        await file3.DisposeAsync();
        
        // Assert - File should now be fully released
        // Opening again should create a new instance
        await using var newFile = await _sut.OpenFileAsync(path);
        newFile.Should().NotBeNull();
    }

    [Fact]
    public async Task ConcurrentAccess_HandlesCorrectly()
    {
        // Arrange
        var content = "Concurrent access test content";
        var path = CreateTestFile(content);
        var tasks = new List<Task<string>>();

        // Act - Multiple concurrent reads
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                await using var file = await _sut.OpenFileAsync(path);
                var data = await file.ReadAsync(0, content.Length);
                return Encoding.UTF8.GetString(data.Span);
            }));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllSatisfy(r => r.Should().Be(content));
    }

    [Fact]
    public async Task DisposeAsync_ClosesAllFiles()
    {
        // Arrange
        var path1 = CreateTestFile("file1");
        var path2 = CreateTestFile("file2");
        
        var file1 = await _sut.OpenFileAsync(path1);
        var file2 = await _sut.OpenFileAsync(path2);

        // Act
        await _sut.DisposeAsync();

        // Assert - Files should be released
        // Trying to use handler after disposal should throw
        await Assert.ThrowsAsync<ObjectDisposedException>(() => 
            _sut.OpenFileAsync(path1));
    }

    [Fact]
    public async Task DisposeAsync_MultipleDispose_HandlesGracefully()
    {
        // Act & Assert
        await _sut.DisposeAsync();
        await _sut.DisposeAsync(); // Should not throw
    }

    [Fact]
    public async Task LargeFileProcessing_HandlesEfficiently()
    {
        // Arrange
        var path = CreateLargeTestFile(500); // 500KB file
        var totalBytes = 0;

        // Act
        var result = await _sut.ProcessFileInParallelAsync(
            path,
            (chunk, index) =>
            {
                Interlocked.Add(ref totalBytes, chunk.Length);
                return Task.FromResult(chunk.Length);
            },
            results => results.Sum(),
            chunkSizeKb: 50);

        // Assert
        result.Should().BeGreaterThan(500 * 1024 * 0.9); // At least 90% of expected size
        totalBytes.Should().Be(result);
    }

    public void Dispose()
    {
        _sut?.DisposeAsync().AsTask().Wait();

        // Clean up test files and directory
        foreach (var file in _testFiles)
        {
            try { File.Delete(file); } catch { /* Best effort */ }
        }

        if (Directory.Exists(_testDirectory))
        {
            try { Directory.Delete(_testDirectory, true); } catch { /* Best effort */ }
        }
    }
}