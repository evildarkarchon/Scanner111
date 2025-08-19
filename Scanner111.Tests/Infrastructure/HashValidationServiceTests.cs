using Scanner111.Core.Infrastructure;
using Scanner111.Tests.TestHelpers;

namespace Scanner111.Tests.Infrastructure;

/// <summary>
///     Contains unit tests for the <see cref="HashValidationService" /> class
/// </summary>
[Collection("IO Heavy Tests")]
public class HashValidationServiceTests : IDisposable
{
    private readonly HashValidationService _hashService;
    private readonly List<string> _tempFiles;
    private readonly TestFileSystem _fileSystem;
    private readonly TestFileVersionInfoProvider _fileVersionInfo;

    public HashValidationServiceTests()
    {
        var logger = NullLogger<HashValidationService>.Instance;
        _fileSystem = new TestFileSystem();
        _fileVersionInfo = new TestFileVersionInfoProvider();
        _hashService = new HashValidationService(logger, _fileSystem, _fileVersionInfo);
        _tempFiles = new List<string>();
    }

    public void Dispose()
    {
        // Clean up temporary files
        foreach (var file in _tempFiles)
            if (File.Exists(file))
                File.Delete(file);

        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task CalculateFileHashAsync_WithValidFile_ReturnsValidHash()
    {
        // Arrange
        var testFile = CreateTempFile("Hello, World!");

        // Act
        var actualHash = await _hashService.CalculateFileHashAsync(testFile);

        // Assert
        actualHash.Should().NotBeNull("because the hash calculation should succeed");
        actualHash.Should().NotBeEmpty("because a valid hash should be returned");
        // SHA256 produces 64 character hex string
        actualHash.Should().HaveLength(64, "because SHA256 produces a 64 character hex string");
        // Should be all uppercase hex characters
        actualHash.Should().MatchRegex("^[A-F0-9]+$", "because hash should contain only uppercase hex characters");
    }

    [Fact]
    public async Task CalculateFileHashAsync_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var nonExistentFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        // Act & Assert
        var act = () => _hashService.CalculateFileHashAsync(nonExistentFile);
        await act.Should().ThrowAsync<FileNotFoundException>("because the file does not exist");
    }

    [Fact]
    public async Task CalculateFileHashAsync_CachesResult_ReturnsSameHashForUnmodifiedFile()
    {
        // Arrange
        var testFile = CreateTempFile("Test content for caching");

        // Act - Calculate hash twice
        var hash1 = await _hashService.CalculateFileHashAsync(testFile);
        var hash2 = await _hashService.CalculateFileHashAsync(testFile);

        // Assert - Both hashes should be identical
        hash2.Should().Be(hash1, "because the file has not been modified and caching should return the same hash");
    }

    [Fact]
    public async Task CalculateFileHashAsync_FileModified_RecalculatesHash()
    {
        // Arrange
        var testFile = CreateTempFile("Initial content");
        var hash1 = await _hashService.CalculateFileHashAsync(testFile);

        // Act - Modify file
        await Task.Delay(10); // Ensure file timestamp changes
        
        // Update in test file system
        _fileSystem.AddFile(testFile, "Modified content");
        _fileSystem.SetLastWriteTime(testFile, DateTime.Now);
        
        // Also update real file for consistency
        File.WriteAllText(testFile, "Modified content");
        
        var hash2 = await _hashService.CalculateFileHashAsync(testFile);

        // Assert - Hashes should be different
        hash2.Should().NotBe(hash1, "because the file was modified and should produce a different hash");
    }

    [Fact]
    public async Task ValidateFileAsync_WithMatchingHash_ReturnsValid()
    {
        // Arrange
        var testFile = CreateTempFile("Test validation content");
        var expectedHash = await _hashService.CalculateFileHashAsync(testFile);

        // Act
        var validation = await _hashService.ValidateFileAsync(testFile, expectedHash);

        // Assert
        validation.IsValid.Should().BeTrue("because the hash matches the expected value");
        validation.ExpectedHash.Should().Be(expectedHash, "because the expected hash should be recorded");
        validation.ActualHash.Should().Be(expectedHash, "because the actual hash should match");
        validation.HashType.Should().Be("SHA256", "because SHA256 is the algorithm used");
    }

    [Fact]
    public async Task ValidateFileAsync_WithNonMatchingHash_ReturnsInvalid()
    {
        // Arrange
        var testFile = CreateTempFile("Test validation content");
        var wrongHash = "0000000000000000000000000000000000000000000000000000000000000000";

        // Act
        var validation = await _hashService.ValidateFileAsync(testFile, wrongHash);

        // Assert
        validation.IsValid.Should().BeFalse("because the hash does not match");
        validation.ExpectedHash.Should().Be(wrongHash, "because the expected hash should be recorded");
        validation.ActualHash.Should().NotBe(wrongHash, "because the actual hash is different");
    }

    [Fact]
    public async Task ValidateFileAsync_WithNonExistentFile_ReturnsInvalid()
    {
        // Arrange
        var nonExistentFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var expectedHash = "ABCD1234";

        // Act
        var validation = await _hashService.ValidateFileAsync(nonExistentFile, expectedHash);

        // Assert
        validation.IsValid.Should().BeFalse("because the file does not exist");
        validation.ActualHash.Should().Be(string.Empty, "because no hash can be calculated for a non-existent file");
    }

    [Fact]
    public async Task ValidateBatchAsync_WithMixedFiles_ReturnsCorrectResults()
    {
        // Arrange
        var file1 = CreateTempFile("File 1 content");
        var file2 = CreateTempFile("File 2 content");
        var nonExistent = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        var hash1 = await _hashService.CalculateFileHashAsync(file1);
        var hash2 = await _hashService.CalculateFileHashAsync(file2);

        var fileHashMap = new Dictionary<string, string>
        {
            [file1] = hash1,
            [file2] = "WRONG_HASH",
            [nonExistent] = "SOME_HASH"
        };

        // Act
        var results = await _hashService.ValidateBatchAsync(fileHashMap);

        // Assert
        results.Should().HaveCount(3, "because three files were validated");
        results[file1].IsValid.Should().BeTrue("because the first file hash matches");
        results[file2].IsValid.Should().BeFalse("because the second file hash is wrong");
        results[nonExistent].IsValid.Should().BeFalse("because the third file does not exist");
    }

    [Fact]
    public async Task CalculateFileHashWithProgressAsync_ReportsProgress()
    {
        // Arrange
        var largeContent = new string('A', 2 * 1024 * 1024); // 2MB file
        var testFile = CreateTempFile(largeContent);
        var progressReports = new List<long>();
        var progress = new Progress<long>(bytes => progressReports.Add(bytes));

        // Act
        var hash = await _hashService.CalculateFileHashWithProgressAsync(testFile, progress);

        // Assert
        hash.Should().NotBeEmpty("because a valid hash should be calculated");
        progressReports.Should().NotBeEmpty("because progress should be reported during hash calculation");
        progressReports.Last().Should()
            .BeGreaterThanOrEqualTo(largeContent.Length, "because all bytes should be processed");
    }

    [Fact]
    public async Task CalculateFileHashAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var largeContent = new string('B', 5 * 1024 * 1024); // 5MB file
        var testFile = CreateTempFile(largeContent);
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancel the token

        // Act & Assert
        var act = () => _hashService.CalculateFileHashAsync(testFile, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>("because the operation was cancelled");
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("The quick brown fox jumps over the lazy dog")]
    [InlineData("1234567890!@#$%^&*()")]
    public async Task CalculateFileHashAsync_WithVariousContent_ProducesConsistentHashes(string content)
    {
        // Arrange
        var testFile = CreateTempFile(content);

        // Act
        var hash1 = await _hashService.CalculateFileHashAsync(testFile);
        var hash2 = await _hashService.CalculateFileHashAsync(testFile);

        // Assert - Same file should produce same hash
        hash2.Should().Be(hash1, "because the same file should produce consistent hashes");
        hash1.Should().HaveLength(64, "because SHA256 produces a 64 character hex string");
    }

    private string CreateTempFile(string content)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        
        // Add to test file system
        _fileSystem.AddFile(tempFile, content);
        
        // Also create real file for cleanup tracking
        File.WriteAllText(tempFile, content, Encoding.UTF8);
        _tempFiles.Add(tempFile);
        return tempFile;
    }
}