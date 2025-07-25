using Microsoft.Extensions.Logging.Abstractions;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using System.Security.Cryptography;
using System.Text;

namespace Scanner111.Tests.Infrastructure;

/// <summary>
/// Contains unit tests for the <see cref="HashValidationService"/> class
/// </summary>
public class HashValidationServiceTests : IDisposable
{
    private readonly HashValidationService _hashService;
    private readonly List<string> _tempFiles;
    
    public HashValidationServiceTests()
    {
        var logger = NullLogger<HashValidationService>.Instance;
        _hashService = new HashValidationService(logger);
        _tempFiles = new List<string>();
    }
    
    public void Dispose()
    {
        // Clean up temporary files
        foreach (var file in _tempFiles)
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
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
        Assert.NotNull(actualHash);
        Assert.NotEmpty(actualHash);
        // SHA256 produces 64 character hex string
        Assert.Equal(64, actualHash.Length);
        // Should be all uppercase hex characters
        Assert.Matches("^[A-F0-9]+$", actualHash);
    }
    
    [Fact]
    public async Task CalculateFileHashAsync_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var nonExistentFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        
        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _hashService.CalculateFileHashAsync(nonExistentFile));
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
        Assert.Equal(hash1, hash2);
    }
    
    [Fact]
    public async Task CalculateFileHashAsync_FileModified_RecalculatesHash()
    {
        // Arrange
        var testFile = CreateTempFile("Initial content");
        var hash1 = await _hashService.CalculateFileHashAsync(testFile);
        
        // Act - Modify file
        await Task.Delay(10); // Ensure file timestamp changes
        File.WriteAllText(testFile, "Modified content");
        var hash2 = await _hashService.CalculateFileHashAsync(testFile);
        
        // Assert - Hashes should be different
        Assert.NotEqual(hash1, hash2);
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
        Assert.True(validation.IsValid);
        Assert.Equal(expectedHash, validation.ExpectedHash);
        Assert.Equal(expectedHash, validation.ActualHash);
        Assert.Equal("SHA256", validation.HashType);
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
        Assert.False(validation.IsValid);
        Assert.Equal(wrongHash, validation.ExpectedHash);
        Assert.NotEqual(wrongHash, validation.ActualHash);
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
        Assert.False(validation.IsValid);
        Assert.Equal(string.Empty, validation.ActualHash);
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
        Assert.Equal(3, results.Count);
        Assert.True(results[file1].IsValid);
        Assert.False(results[file2].IsValid);
        Assert.False(results[nonExistent].IsValid);
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
        Assert.NotEmpty(hash);
        Assert.NotEmpty(progressReports);
        Assert.True(progressReports.Last() >= largeContent.Length);
    }
    
    [Fact]
    public async Task CalculateFileHashAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var largeContent = new string('B', 5 * 1024 * 1024); // 5MB file
        var testFile = CreateTempFile(largeContent);
        var cts = new CancellationTokenSource();
        
        // Act
        var task = _hashService.CalculateFileHashAsync(testFile, cts.Token);
        cts.Cancel();
        
        // Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => task);
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
        Assert.Equal(hash1, hash2);
        Assert.Equal(64, hash1.Length); // SHA256 produces 64 char hex string
    }
    
    private string CreateTempFile(string content)
    {
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, content, Encoding.UTF8);
        _tempFiles.Add(tempFile);
        return tempFile;
    }
}