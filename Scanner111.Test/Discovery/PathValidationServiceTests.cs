using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Scanner111.Core.Discovery;

namespace Scanner111.Test.Discovery;

[Trait("Category", "Unit")]
[Trait("Performance", "Fast")]
[Trait("Component", "Discovery")]
public class PathValidationServiceTests : IDisposable
{
    private readonly ILogger<PathValidationService> _logger;
    private readonly PathValidationService _sut;
    private readonly string _tempDirectory;
    private readonly string _tempFile;

    public PathValidationServiceTests()
    {
        _logger = Substitute.For<ILogger<PathValidationService>>();
        _sut = new PathValidationService(_logger);

        _tempDirectory = Path.Combine(Path.GetTempPath(), $"Scanner111Test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);

        _tempFile = Path.Combine(_tempDirectory, "test.txt");
        File.WriteAllText(_tempFile, "test content");
    }

    public void Dispose()
    {
        _sut?.Dispose();
        if (Directory.Exists(_tempDirectory)) Directory.Delete(_tempDirectory, true);
    }

    [Fact]
    public async Task ValidatePathAsync_WithValidDirectory_ReturnsSuccess()
    {
        // Act
        var result = await _sut.ValidatePathAsync(_tempDirectory);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.Path.Should().Be(_tempDirectory);
        result.Exists.Should().BeTrue();
        result.CanRead.Should().BeTrue();
    }

    [Fact]
    public async Task ValidatePathAsync_WithValidFile_ReturnsSuccess()
    {
        // Act
        var result = await _sut.ValidatePathAsync(_tempFile);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.Path.Should().Be(_tempFile);
        result.Exists.Should().BeTrue();
        result.CanRead.Should().BeTrue();
    }

    [Fact]
    public async Task ValidatePathAsync_WithNonExistentPath_ReturnsFailure()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_tempDirectory, "nonexistent.txt");

        // Act
        var result = await _sut.ValidatePathAsync(nonExistentPath);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Path.Should().Be(nonExistentPath);
        result.Exists.Should().BeFalse();
        result.ErrorMessage.Should().Contain("does not exist");
    }

    [Fact]
    public async Task ValidatePathAsync_WithNullPath_ReturnsFailure()
    {
        // Act
        var result = await _sut.ValidatePathAsync(null!);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("null or empty");
    }

    [Fact]
    public async Task ValidatePathAsync_WithEmptyPath_ReturnsFailure()
    {
        // Act
        var result = await _sut.ValidatePathAsync(string.Empty);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("null or empty");
    }

    [Fact]
    public async Task ValidatePathsAsync_WithMultiplePaths_ValidatesAll()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_tempDirectory, "nonexistent.txt");
        var paths = new[] { _tempDirectory, _tempFile, nonExistentPath };

        // Act
        var results = await _sut.ValidatePathsAsync(paths);

        // Assert
        results.Should().HaveCount(3);
        results[_tempDirectory].IsValid.Should().BeTrue();
        results[_tempFile].IsValid.Should().BeTrue();
        results[nonExistentPath].IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task FileExistsAsync_WithExistingFile_ReturnsTrue()
    {
        // Act
        var exists = await _sut.FileExistsAsync(_tempFile);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task FileExistsAsync_WithNonExistentFile_ReturnsFalse()
    {
        // Arrange
        var nonExistentFile = Path.Combine(_tempDirectory, "nonexistent.txt");

        // Act
        var exists = await _sut.FileExistsAsync(nonExistentFile);

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task FileExistsAsync_WithDirectory_ReturnsFalse()
    {
        // Act
        var exists = await _sut.FileExistsAsync(_tempDirectory);

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task DirectoryExistsAsync_WithExistingDirectory_ReturnsTrue()
    {
        // Act
        var exists = await _sut.DirectoryExistsAsync(_tempDirectory);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task DirectoryExistsAsync_WithNonExistentDirectory_ReturnsFalse()
    {
        // Arrange
        var nonExistentDir = Path.Combine(_tempDirectory, "nonexistent");

        // Act
        var exists = await _sut.DirectoryExistsAsync(nonExistentDir);

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task DirectoryExistsAsync_WithFile_ReturnsFalse()
    {
        // Act
        var exists = await _sut.DirectoryExistsAsync(_tempFile);

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task HasReadAccessAsync_WithReadableFile_ReturnsTrue()
    {
        // Act
        var hasAccess = await _sut.HasReadAccessAsync(_tempFile);

        // Assert
        hasAccess.Should().BeTrue();
    }

    [Fact]
    public async Task HasWriteAccessAsync_WithWritableFile_ReturnsTrue()
    {
        // Act
        var hasAccess = await _sut.HasWriteAccessAsync(_tempFile);

        // Assert
        hasAccess.Should().BeTrue();
    }

    [Fact]
    public void NormalizePath_WithRelativePath_ReturnsAbsolutePath()
    {
        // Arrange
        var relativePath = @"..\test\file.txt";

        // Act
        var normalized = _sut.NormalizePath(relativePath);

        // Assert
        normalized.Should().NotBeNullOrWhiteSpace();
        Path.IsPathRooted(normalized).Should().BeTrue();
    }

    [Fact]
    public void NormalizePath_WithMixedSeparators_ReturnsConsistentPath()
    {
        // Arrange
        var mixedPath = @"C:\Users/test\Documents/file.txt";

        // Act
        var normalized = _sut.NormalizePath(mixedPath);

        // Assert
        normalized.Should().NotContain("/");
        normalized.Should().Contain(Path.DirectorySeparatorChar.ToString());
    }

    [Fact]
    public void IsPathSafe_WithSafePath_ReturnsTrue()
    {
        // Arrange
        var safePath = Path.Combine(_tempDirectory, "safe", "file.txt");

        // Act
        var isSafe = _sut.IsPathSafe(safePath, _tempDirectory);

        // Assert
        isSafe.Should().BeTrue();
    }

    [Fact]
    public void IsPathSafe_WithDirectoryTraversal_ReturnsFalse()
    {
        // Arrange
        var unsafePath = Path.Combine(_tempDirectory, "..", "..", "dangerous.txt");

        // Act
        var isSafe = _sut.IsPathSafe(unsafePath, _tempDirectory);

        // Assert
        isSafe.Should().BeFalse();
    }

    [Fact]
    public async Task GetCachedResult_WithCachedPath_ReturnsCachedResult()
    {
        // Arrange
        await _sut.ValidatePathAsync(_tempDirectory);

        // Act
        var cachedResult = _sut.GetCachedResult(_tempDirectory);

        // Assert
        cachedResult.Should().NotBeNull();
        cachedResult!.IsValid.Should().BeTrue();
        cachedResult.Path.Should().Be(_tempDirectory);
    }

    [Fact]
    public async Task GetCachedResult_AfterClearCache_ReturnsNull()
    {
        // Arrange
        await _sut.ValidatePathAsync(_tempDirectory);
        _sut.ClearCache();

        // Act
        var cachedResult = _sut.GetCachedResult(_tempDirectory);

        // Assert
        cachedResult.Should().BeNull();
    }

    [Fact]
    public async Task SetCacheExpiration_WithZeroExpiration_DisablesCaching()
    {
        // Arrange
        _sut.SetCacheExpiration(TimeSpan.Zero);
        await _sut.ValidatePathAsync(_tempDirectory);

        // Act
        var cachedResult = _sut.GetCachedResult(_tempDirectory);

        // Assert
        cachedResult.Should().BeNull();
    }

    [Fact]
    public async Task ValidatePathAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            _sut.ValidatePathAsync(_tempDirectory, cancellationToken: cts.Token));
    }

    [Fact]
    public async Task ValidatePathsAsync_ConcurrentCalls_HandledSafely()
    {
        // Arrange
        var paths = Enumerable.Range(0, 10)
            .Select(i => Path.Combine(_tempDirectory, $"test{i}.txt"))
            .ToList();

        foreach (var path in paths) await File.WriteAllTextAsync(path, "test");

        // Act - Make concurrent validation calls
        var tasks = paths.Select(p => _sut.ValidatePathAsync(p)).ToList();
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllSatisfy(r =>
        {
            r.Should().NotBeNull();
            r.IsValid.Should().BeTrue();
        });
    }
}