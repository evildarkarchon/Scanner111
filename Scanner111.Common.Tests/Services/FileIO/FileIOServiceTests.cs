using FluentAssertions;
using Scanner111.Common.Services.FileIO;

namespace Scanner111.Common.Tests.Services.FileIO;

/// <summary>
/// Tests for FileIOService.
/// </summary>
public class FileIOServiceTests
{
    private readonly FileIOService _service;

    public FileIOServiceTests()
    {
        _service = new FileIOService();
    }

    [Fact]
    public async Task ReadFileAsync_WithValidFile_ReturnsContent()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var expectedContent = "Test content\nLine 2\nLine 3";
        await File.WriteAllTextAsync(tempFile, expectedContent);

        try
        {
            // Act
            var content = await _service.ReadFileAsync(tempFile);

            // Assert
            content.Should().Be(expectedContent);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ReadFileAsync_WithNonExistentFile_ThrowsException()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        // Act
        var act = () => _service.ReadFileAsync(nonExistentPath);

        // Assert
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task WriteFileAsync_CreatesFileWithContent()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var content = "Test content for writing";

        try
        {
            // Act
            await _service.WriteFileAsync(tempFile, content);

            // Assert
            File.Exists(tempFile).Should().BeTrue();
            var writtenContent = await File.ReadAllTextAsync(tempFile);
            writtenContent.Should().Be(content);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task WriteFileAsync_OverwritesExistingFile()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, "Original content");
        var newContent = "New content";

        try
        {
            // Act
            await _service.WriteFileAsync(tempFile, newContent);

            // Assert
            var writtenContent = await File.ReadAllTextAsync(tempFile);
            writtenContent.Should().Be(newContent);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task FileExistsAsync_WithExistingFile_ReturnsTrue()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();

        try
        {
            // Act
            var exists = await _service.FileExistsAsync(tempFile);

            // Assert
            exists.Should().BeTrue();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task FileExistsAsync_WithNonExistentFile_ReturnsFalse()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        // Act
        var exists = await _service.FileExistsAsync(nonExistentPath);

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task ReadFileAsync_WithMalformedUtf8_HandlesGracefully()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        // Write some invalid UTF-8 bytes
        await File.WriteAllBytesAsync(tempFile, new byte[] { 0xFF, 0xFE, 0xFD });

        try
        {
            // Act
            var content = await _service.ReadFileAsync(tempFile);

            // Assert
            // Should not throw exception - UTF-8 error handling is enabled
            content.Should().NotBeNull();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
