using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Scanner111.Core.IO;

namespace Scanner111.Test.IO;

public sealed class HighPerformanceFileIOTests : IDisposable
{
    private readonly ILogger<HighPerformanceFileIO> _logger;
    private readonly MemoryMappedFileHandler _memoryMappedHandler;
    private readonly HighPerformanceFileIO _sut;
    private readonly string _testDirectory;
    private readonly List<string> _testFiles;

    public HighPerformanceFileIOTests()
    {
        _logger = Substitute.For<ILogger<HighPerformanceFileIO>>();
        var mmfLogger = Substitute.For<ILogger<MemoryMappedFileHandler>>();
        _memoryMappedHandler = new MemoryMappedFileHandler(mmfLogger);
        _sut = new HighPerformanceFileIO(_logger, _memoryMappedHandler);

        _testDirectory = Path.Combine(Path.GetTempPath(), $"HighPerfIO_{Guid.NewGuid():N}");
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

    private string CreateLargeTestFile(int sizeInMb, string? fileName = null)
    {
        fileName ??= $"large_{Guid.NewGuid():N}.txt";
        var path = Path.Combine(_testDirectory, fileName);
        
        // Create a file larger than 1MB to trigger memory-mapped reading
        var sb = new StringBuilder();
        var line = new string('X', 1024); // 1KB line
        for (int i = 0; i < sizeInMb * 1024; i++)
        {
            sb.AppendLine($"Line {i}: {line}");
        }
        
        File.WriteAllText(path, sb.ToString());
        _testFiles.Add(path);
        return path;
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new HighPerformanceFileIO(null!, _memoryMappedHandler);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void Constructor_NullMemoryMappedHandler_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new HighPerformanceFileIO(_logger, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("memoryMappedHandler");
    }

    [Fact]
    public async Task ReadFileAsync_SmallFile_ReadsCorrectly()
    {
        // Arrange
        var content = "This is a test file content.\nWith multiple lines.\nAnd UTF-8 encoding.";
        var path = CreateTestFile(content);

        // Act
        var result = await _sut.ReadFileAsync(path);

        // Assert
        result.Should().Be(content);
    }

    [Fact]
    public async Task ReadFileAsync_LargeFile_UsesMemoryMapping()
    {
        // Arrange
        var path = CreateLargeTestFile(2); // 2MB file

        // Act
        var result = await _sut.ReadFileAsync(path);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain("Line 0:");
        result.Should().Contain("Line 100:");
    }

    [Fact]
    public async Task ReadFileAsync_EmptyFile_ReturnsEmptyString()
    {
        // Arrange
        var path = CreateTestFile("");

        // Act
        var result = await _sut.ReadFileAsync(path);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadFileAsync_NonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var path = Path.Combine(_testDirectory, "nonexistent.txt");

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() => _sut.ReadFileAsync(path));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task ReadFileAsync_InvalidPath_ThrowsArgumentException(string? path)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.ReadFileAsync(path!));
    }

    [Fact]
    public async Task ReadFileAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var path = CreateLargeTestFile(5); // Large file for longer operation
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => 
            _sut.ReadFileAsync(path, cts.Token));
    }

    [Fact]
    public async Task ReadLinesAsync_SmallFile_ReturnsAllLines()
    {
        // Arrange
        var lines = new[] { "Line 1", "Line 2", "Line 3" };
        var content = string.Join(Environment.NewLine, lines);
        var path = CreateTestFile(content);

        // Act
        var result = await _sut.ReadLinesAsync(path);

        // Assert
        result.Should().BeEquivalentTo(lines);
    }

    [Fact]
    public async Task ReadLinesAsync_LargeFile_UsesMemoryMapping()
    {
        // Arrange
        var path = CreateLargeTestFile(2);

        // Act
        var result = await _sut.ReadLinesAsync(path);

        // Assert
        result.Should().NotBeEmpty();
        result.Length.Should().BeGreaterThan(1000);
        result[0].Should().StartWith("Line 0:");
    }

    [Fact]
    public async Task ReadLinesAsync_EmptyFile_ReturnsEmptyArray()
    {
        // Arrange
        var path = CreateTestFile("");

        // Act
        var result = await _sut.ReadLinesAsync(path);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task WriteFileAsync_SimpleContent_WritesCorrectly()
    {
        // Arrange
        var path = Path.Combine(_testDirectory, "write_test.txt");
        var content = "Test content to write";

        // Act
        await _sut.WriteFileAsync(path, content);

        // Assert
        File.Exists(path).Should().BeTrue();
        var written = await File.ReadAllTextAsync(path);
        written.Should().Be(content);
        _testFiles.Add(path);
    }

    [Fact]
    public async Task WriteFileAsync_CreatesDirectory_WhenNotExists()
    {
        // Arrange
        var subDir = Path.Combine(_testDirectory, "subdir");
        var path = Path.Combine(subDir, "file.txt");
        var content = "Content in subdirectory";

        // Act
        await _sut.WriteFileAsync(path, content);

        // Assert
        Directory.Exists(subDir).Should().BeTrue();
        File.Exists(path).Should().BeTrue();
        var written = await File.ReadAllTextAsync(path);
        written.Should().Be(content);
        _testFiles.Add(path);
    }

    [Fact]
    public async Task WriteFileAsync_AtomicWrite_OverwritesExisting()
    {
        // Arrange
        var path = CreateTestFile("Original content");
        var newContent = "New content";

        // Act
        await _sut.WriteFileAsync(path, newContent);

        // Assert
        var written = await File.ReadAllTextAsync(path);
        written.Should().Be(newContent);
    }

    [Fact]
    public async Task WriteFileAsync_WithEncoding_UsesCorrectEncoding()
    {
        // Arrange
        var path = Path.Combine(_testDirectory, "encoded.txt");
        var content = "Content with special chars: ñ, é, ü";
        var encoding = Encoding.Unicode;

        // Act
        await _sut.WriteFileAsync(path, content, encoding);

        // Assert
        var bytes = await File.ReadAllBytesAsync(path);
        var written = encoding.GetString(bytes);
        written.Should().Be(content);
        _testFiles.Add(path);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task WriteFileAsync_InvalidPath_ThrowsArgumentException(string? path)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _sut.WriteFileAsync(path!, "content"));
    }

    [Fact]
    public async Task WriteFileAsync_NullContent_ThrowsArgumentNullException()
    {
        // Arrange
        var path = Path.Combine(_testDirectory, "file.txt");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            _sut.WriteFileAsync(path, null!));
    }

    [Fact]
    public async Task WriteLinesAsync_WritesLinesCorrectly()
    {
        // Arrange
        var path = Path.Combine(_testDirectory, "lines.txt");
        var lines = new[] { "Line 1", "Line 2", "Line 3" };

        // Act
        await _sut.WriteLinesAsync(path, lines);

        // Assert
        var written = await File.ReadAllLinesAsync(path);
        written.Should().BeEquivalentTo(lines);
        _testFiles.Add(path);
    }

    [Fact]
    public async Task FileExistsAsync_ExistingFile_ReturnsTrue()
    {
        // Arrange
        var path = CreateTestFile("content");

        // Act
        var exists = await _sut.FileExistsAsync(path);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task FileExistsAsync_NonExistentFile_ReturnsFalse()
    {
        // Arrange
        var path = Path.Combine(_testDirectory, "nonexistent.txt");

        // Act
        var exists = await _sut.FileExistsAsync(path);

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task DirectoryExistsAsync_ExistingDirectory_ReturnsTrue()
    {
        // Act
        var exists = await _sut.DirectoryExistsAsync(_testDirectory);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task DirectoryExistsAsync_NonExistentDirectory_ReturnsFalse()
    {
        // Arrange
        var path = Path.Combine(_testDirectory, "nonexistent");

        // Act
        var exists = await _sut.DirectoryExistsAsync(path);

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task GetLastWriteTimeAsync_ExistingFile_ReturnsCorrectTime()
    {
        // Arrange
        var path = CreateTestFile("content");
        var expectedTime = File.GetLastWriteTimeUtc(path);

        // Act
        var result = await _sut.GetLastWriteTimeAsync(path);

        // Assert
        result.Should().NotBeNull();
        result.Value.Should().BeCloseTo(expectedTime, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task GetLastWriteTimeAsync_NonExistentFile_ReturnsNull()
    {
        // Arrange
        var path = Path.Combine(_testDirectory, "nonexistent.txt");

        // Act
        var result = await _sut.GetLastWriteTimeAsync(path);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateDirectoryAsync_CreatesDirectory()
    {
        // Arrange
        var path = Path.Combine(_testDirectory, "newdir");

        // Act
        await _sut.CreateDirectoryAsync(path);

        // Assert
        Directory.Exists(path).Should().BeTrue();
    }

    [Fact]
    public async Task CreateDirectoryAsync_ExistingDirectory_DoesNotThrow()
    {
        // Act & Assert
        await _sut.CreateDirectoryAsync(_testDirectory);
        Directory.Exists(_testDirectory).Should().BeTrue();
    }

    [Fact]
    public async Task DeleteFileAsync_ExistingFile_DeletesAndReturnsTrue()
    {
        // Arrange
        var path = CreateTestFile("to delete");

        // Act
        var result = await _sut.DeleteFileAsync(path);

        // Assert
        result.Should().BeTrue();
        File.Exists(path).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteFileAsync_NonExistentFile_ReturnsFalse()
    {
        // Arrange
        var path = Path.Combine(_testDirectory, "nonexistent.txt");

        // Act
        var result = await _sut.DeleteFileAsync(path);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CopyFileAsync_CopiesFileCorrectly()
    {
        // Arrange
        var content = "Content to copy";
        var source = CreateTestFile(content, "source.txt");
        var dest = Path.Combine(_testDirectory, "dest.txt");

        // Act
        await _sut.CopyFileAsync(source, dest);

        // Assert
        File.Exists(dest).Should().BeTrue();
        var copied = await File.ReadAllTextAsync(dest);
        copied.Should().Be(content);
        _testFiles.Add(dest);
    }

    [Fact]
    public async Task CopyFileAsync_WithOverwrite_OverwritesExisting()
    {
        // Arrange
        var source = CreateTestFile("New content", "source.txt");
        var dest = CreateTestFile("Old content", "dest.txt");

        // Act
        await _sut.CopyFileAsync(source, dest, overwrite: true);

        // Assert
        var copied = await File.ReadAllTextAsync(dest);
        copied.Should().Be("New content");
    }

    [Fact]
    public async Task CopyFileAsync_WithoutOverwrite_ThrowsIfExists()
    {
        // Arrange
        var source = CreateTestFile("content", "source.txt");
        var dest = CreateTestFile("existing", "dest.txt");

        // Act & Assert
        await Assert.ThrowsAsync<IOException>(() => 
            _sut.CopyFileAsync(source, dest, overwrite: false));
    }

    [Fact]
    public async Task DetectEncodingAsync_UTF8WithBOM_DetectsCorrectly()
    {
        // Arrange
        var path = Path.Combine(_testDirectory, "utf8bom.txt");
        var preamble = Encoding.UTF8.GetPreamble();
        var content = Encoding.UTF8.GetBytes("UTF-8 content");
        await File.WriteAllBytesAsync(path, preamble.Concat(content).ToArray());
        _testFiles.Add(path);

        // Act
        var encoding = await _sut.DetectEncodingAsync(path);

        // Assert
        encoding.Should().Be(Encoding.UTF8);
    }

    [Fact]
    public async Task DetectEncodingAsync_UTF16LE_DetectsCorrectly()
    {
        // Arrange
        var path = Path.Combine(_testDirectory, "utf16le.txt");
        await File.WriteAllBytesAsync(path, new byte[] { 0xFF, 0xFE, 0x41, 0x00 });
        _testFiles.Add(path);

        // Act
        var encoding = await _sut.DetectEncodingAsync(path);

        // Assert
        encoding.Should().Be(Encoding.Unicode);
    }

    [Fact]
    public async Task DetectEncodingAsync_UTF16BE_DetectsCorrectly()
    {
        // Arrange
        var path = Path.Combine(_testDirectory, "utf16be.txt");
        await File.WriteAllBytesAsync(path, new byte[] { 0xFE, 0xFF, 0x00, 0x41 });
        _testFiles.Add(path);

        // Act
        var encoding = await _sut.DetectEncodingAsync(path);

        // Assert
        encoding.Should().Be(Encoding.BigEndianUnicode);
    }

    [Fact]
    public async Task DetectEncodingAsync_NoBOM_DefaultsToUTF8()
    {
        // Arrange
        var path = CreateTestFile("Plain text");

        // Act
        var encoding = await _sut.DetectEncodingAsync(path);

        // Assert
        encoding.Should().Be(Encoding.UTF8);
    }

    [Fact]
    public async Task ProcessFilesInParallelAsync_ProcessesAllFiles()
    {
        // Arrange
        var files = new List<string>();
        for (int i = 0; i < 5; i++)
        {
            files.Add(CreateTestFile($"Content {i}", $"file{i}.txt"));
        }

        async Task<int> Processor(string path, string content)
        {
            await Task.Yield();
            return content.Length;
        }

        // Act
        var results = await _sut.ProcessFilesInParallelAsync(files, Processor);

        // Assert
        results.Should().HaveCount(5);
        results.Values.Should().AllSatisfy(length => length.Should().BeGreaterThan(0));
    }

    [Fact]
    public async Task ProcessFilesInParallelAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var files = Enumerable.Range(0, 10)
            .Select(i => CreateTestFile($"Content {i}", $"file{i}.txt"))
            .ToList();

        var cts = new CancellationTokenSource();
        
        async Task<int> Processor(string path, string content)
        {
            await Task.Delay(100);
            return content.Length;
        }

        cts.CancelAfter(50);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _sut.ProcessFilesInParallelAsync(files, Processor, cts.Token));
    }

    [Fact]
    public async Task ReadWithPipelineAsync_ProcessesFileEfficiently()
    {
        // Arrange
        var content = string.Join("\n", Enumerable.Range(0, 1000).Select(i => $"Line {i}"));
        var path = CreateTestFile(content);
        var processedBytes = 0L;

        async Task Processor(ReadOnlySequence<byte> buffer)
        {
            processedBytes += buffer.Length;
            await Task.Yield();
        }

        // Act
        var result = await _sut.ReadWithPipelineAsync(path, Processor);

        // Assert
        result.Should().NotBeNull();
        result.BytesProcessed.Should().BeGreaterThan(0);
        result.ThroughputMBps.Should().BeGreaterThan(0);
        processedBytes.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ReadWithPipelineAsync_EmptyFile_ProcessesCorrectly()
    {
        // Arrange
        var path = CreateTestFile("");
        var called = false;

        async Task Processor(ReadOnlySequence<byte> buffer)
        {
            if (buffer.Length > 0)
                called = true;
            await Task.Yield();
        }

        // Act
        var result = await _sut.ReadWithPipelineAsync(path, Processor);

        // Assert
        result.BytesProcessed.Should().Be(0);
        called.Should().BeFalse();
    }

    [Fact]
    public async Task DisposeAsync_DisposesResourcesCorrectly()
    {
        // Arrange
        var path = CreateTestFile("content");
        await _sut.ReadFileAsync(path);

        // Act
        await _sut.DisposeAsync();

        // Assert - Second dispose should not throw
        await _sut.DisposeAsync();
    }

    [Fact]
    public async Task ConcurrentReads_HandleCorrectly()
    {
        // Arrange
        var content = "Concurrent test content";
        var path = CreateTestFile(content);
        var tasks = new List<Task<string>>();

        // Act - Perform multiple concurrent reads
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(_sut.ReadFileAsync(path));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllSatisfy(r => r.Should().Be(content));
    }

    [Fact]
    public async Task AtomicWrite_HandlesFailureCorrectly()
    {
        // Arrange
        var path = CreateTestFile("original");
        
        // Make file read-only to cause write failure
        File.SetAttributes(path, FileAttributes.ReadOnly);

        try
        {
            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => 
                _sut.WriteFileAsync(path, "new content"));

            // Original file should still exist with original content
            File.SetAttributes(path, FileAttributes.Normal);
            var content = await File.ReadAllTextAsync(path);
            content.Should().Be("original");
        }
        finally
        {
            // Cleanup
            File.SetAttributes(path, FileAttributes.Normal);
        }
    }

    public void Dispose()
    {
        _sut?.DisposeAsync().AsTask().Wait();
        _memoryMappedHandler?.DisposeAsync().AsTask().Wait();

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