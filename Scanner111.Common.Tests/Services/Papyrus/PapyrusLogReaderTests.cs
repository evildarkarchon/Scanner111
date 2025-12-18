using FluentAssertions;
using Scanner111.Common.Models.Papyrus;
using Scanner111.Common.Services.Papyrus;

namespace Scanner111.Common.Tests.Services.Papyrus;

/// <summary>
/// Tests for PapyrusLogReader.
/// </summary>
public class PapyrusLogReaderTests
{
    private readonly PapyrusLogReader _reader;

    public PapyrusLogReaderTests()
    {
        _reader = new PapyrusLogReader();
    }

    [Fact]
    public void GetFileEndPosition_WithValidFile_ReturnsFileLength()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var content = "Test content\nLine 2\nLine 3";
        File.WriteAllText(tempFile, content);

        try
        {
            // Act
            var position = _reader.GetFileEndPosition(tempFile);

            // Assert
            position.Should().Be(new FileInfo(tempFile).Length);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void GetFileEndPosition_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        // Act
        var act = () => _reader.GetFileEndPosition(nonExistentPath);

        // Assert
        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void GetFileEndPosition_WithEmptyPath_ThrowsArgumentException()
    {
        // Act
        var act = () => _reader.GetFileEndPosition("");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task ReadNewContentAsync_WithDumpingStacksPattern_CountsDumps()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var content = """
            [08/15/2024 - 10:00:00AM] Dumping Stacks
            [08/15/2024 - 10:00:01AM] Some other log entry
            [08/15/2024 - 10:00:02AM] Dumping Stacks
            """;
        await File.WriteAllTextAsync(tempFile, content);

        try
        {
            // Act
            var result = await _reader.ReadNewContentAsync(tempFile, 0, PapyrusStats.Empty);

            // Assert
            result.Stats.Dumps.Should().Be(2);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ReadNewContentAsync_WithDumpingStackPattern_CountsStacks()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var content = """
            [08/15/2024 - 10:00:00AM] Dumping Stack 1
            [08/15/2024 - 10:00:01AM] Dumping Stack 2
            [08/15/2024 - 10:00:02AM] Dumping Stack 3
            """;
        await File.WriteAllTextAsync(tempFile, content);

        try
        {
            // Act
            var result = await _reader.ReadNewContentAsync(tempFile, 0, PapyrusStats.Empty);

            // Assert
            result.Stats.Stacks.Should().Be(3);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ReadNewContentAsync_WithWarningPattern_CountsWarnings()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var content = """
            [08/15/2024 - 10:00:00AM] warning: Something went wrong
            [08/15/2024 - 10:00:01AM] Normal log entry
            [08/15/2024 - 10:00:02AM] warning: Another warning
            """;
        await File.WriteAllTextAsync(tempFile, content);

        try
        {
            // Act
            var result = await _reader.ReadNewContentAsync(tempFile, 0, PapyrusStats.Empty);

            // Assert
            result.Stats.Warnings.Should().Be(2);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ReadNewContentAsync_WithErrorPattern_CountsErrors()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var content = """
            [08/15/2024 - 10:00:00AM] error: Critical failure
            [08/15/2024 - 10:00:01AM] Normal log entry
            [08/15/2024 - 10:00:02AM] error: Another error
            [08/15/2024 - 10:00:03AM] error: Third error
            """;
        await File.WriteAllTextAsync(tempFile, content);

        try
        {
            // Act
            var result = await _reader.ReadNewContentAsync(tempFile, 0, PapyrusStats.Empty);

            // Assert
            result.Stats.Errors.Should().Be(3);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ReadNewContentAsync_WithMixedPatterns_CountsAllCorrectly()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var content = """
            [08/15/2024 - 10:00:00AM] Dumping Stacks
            [08/15/2024 - 10:00:01AM] Dumping Stack 1
            [08/15/2024 - 10:00:02AM] Dumping Stack 2
            [08/15/2024 - 10:00:03AM] warning: Something happened
            [08/15/2024 - 10:00:04AM] error: Something bad happened
            [08/15/2024 - 10:00:05AM] Dumping Stacks
            [08/15/2024 - 10:00:06AM] Dumping Stack 3
            """;
        await File.WriteAllTextAsync(tempFile, content);

        try
        {
            // Act
            var result = await _reader.ReadNewContentAsync(tempFile, 0, PapyrusStats.Empty);

            // Assert
            result.Stats.Dumps.Should().Be(2);
            result.Stats.Stacks.Should().Be(3);
            result.Stats.Warnings.Should().Be(1);
            result.Stats.Errors.Should().Be(1);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ReadNewContentAsync_FromStartPosition_OnlyReadsNewContent()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var initialContent = """
            [08/15/2024 - 10:00:00AM] error: Old error
            [08/15/2024 - 10:00:01AM] warning: Old warning
            """;
        await File.WriteAllTextAsync(tempFile, initialContent);
        var startPosition = new FileInfo(tempFile).Length;

        // Append new content
        var newContent = """

            [08/15/2024 - 10:00:02AM] error: New error
            """;
        await File.AppendAllTextAsync(tempFile, newContent);

        try
        {
            // Act
            var result = await _reader.ReadNewContentAsync(tempFile, startPosition, PapyrusStats.Empty);

            // Assert
            result.Stats.Errors.Should().Be(1); // Only the new error
            result.Stats.Warnings.Should().Be(0); // Old warning not counted
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ReadNewContentAsync_AccumulatesWithExistingStats()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var content = """
            [08/15/2024 - 10:00:00AM] error: New error
            [08/15/2024 - 10:00:01AM] warning: New warning
            """;
        await File.WriteAllTextAsync(tempFile, content);

        var existingStats = new PapyrusStats
        {
            Timestamp = DateTime.Now,
            Dumps = 5,
            Stacks = 10,
            Warnings = 3,
            Errors = 2
        };

        try
        {
            // Act
            var result = await _reader.ReadNewContentAsync(tempFile, 0, existingStats);

            // Assert
            result.Stats.Dumps.Should().Be(5); // Unchanged
            result.Stats.Stacks.Should().Be(10); // Unchanged
            result.Stats.Warnings.Should().Be(4); // 3 + 1
            result.Stats.Errors.Should().Be(3); // 2 + 1
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ReadNewContentAsync_WithEmptyFile_ReturnsEmptyStats()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        // File is already empty

        try
        {
            // Act
            var result = await _reader.ReadNewContentAsync(tempFile, 0, PapyrusStats.Empty);

            // Assert
            result.Stats.Dumps.Should().Be(0);
            result.Stats.Stacks.Should().Be(0);
            result.Stats.Warnings.Should().Be(0);
            result.Stats.Errors.Should().Be(0);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ReadNewContentAsync_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        // Act
        var act = () => _reader.ReadNewContentAsync(nonExistentPath, 0, PapyrusStats.Empty);

        // Assert
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task ReadNewContentAsync_ReturnsUpdatedFilePosition()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var content = "Test content\nLine 2";
        await File.WriteAllTextAsync(tempFile, content);

        try
        {
            // Act
            var result = await _reader.ReadNewContentAsync(tempFile, 0, PapyrusStats.Empty);

            // Assert
            result.NewPosition.Should().Be(new FileInfo(tempFile).Length);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ReadNewContentAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        // Create a large file to ensure cancellation has time to occur
        var content = string.Join("\n", Enumerable.Repeat("Some log entry without patterns", 10000));
        await File.WriteAllTextAsync(tempFile, content);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        try
        {
            // Act
            var act = () => _reader.ReadNewContentAsync(tempFile, 0, PapyrusStats.Empty, cts.Token);

            // Assert
            await act.Should().ThrowAsync<OperationCanceledException>();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ReadNewContentAsync_WarningPattern_IsCaseInsensitive()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var content = """
            [08/15/2024 - 10:00:00AM] WARNING: uppercase
            [08/15/2024 - 10:00:01AM] Warning: mixed case
            [08/15/2024 - 10:00:02AM] warning: lowercase
            """;
        await File.WriteAllTextAsync(tempFile, content);

        try
        {
            // Act
            var result = await _reader.ReadNewContentAsync(tempFile, 0, PapyrusStats.Empty);

            // Assert
            result.Stats.Warnings.Should().Be(3);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ReadNewContentAsync_ErrorPattern_IsCaseInsensitive()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var content = """
            [08/15/2024 - 10:00:00AM] ERROR: uppercase
            [08/15/2024 - 10:00:01AM] Error: mixed case
            [08/15/2024 - 10:00:02AM] error: lowercase
            """;
        await File.WriteAllTextAsync(tempFile, content);

        try
        {
            // Act
            var result = await _reader.ReadNewContentAsync(tempFile, 0, PapyrusStats.Empty);

            // Assert
            result.Stats.Errors.Should().Be(3);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
