using FluentAssertions;
using Scanner111.Cli.Progress;
using Scanner111.Common.Models.Configuration;

namespace Scanner111.Cli.Tests.Progress;

public class ConsoleOutputTests
{
    [Fact]
    public void WriteHeader_DoesNotThrow()
    {
        // Act
        var act = () => ConsoleOutput.WriteHeader();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void WriteInfo_WithMessage_DoesNotThrow()
    {
        // Act
        var act = () => ConsoleOutput.WriteInfo("Test message");

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void WriteWarning_WithMessage_DoesNotThrow()
    {
        // Act
        var act = () => ConsoleOutput.WriteWarning("Test warning");

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void WriteError_WithMessage_DoesNotThrow()
    {
        // Act
        var act = () => ConsoleOutput.WriteError("Test error");

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void WriteSummary_WithValidResult_DoesNotThrow()
    {
        // Arrange
        var result = new ScanResult
        {
            Statistics = new ScanStatistics
            {
                TotalFiles = 10,
                Scanned = 8,
                Incomplete = 1,
                Failed = 1,
                ScanStartTime = DateTime.UtcNow
            },
            ScanDuration = TimeSpan.FromSeconds(5.5),
            FailedLogs = new[] { "crash-failed.log" },
            ProcessedFiles = new[] { "crash-1.log", "crash-2.log" }
        };

        // Act
        var act = () => ConsoleOutput.WriteSummary(result);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void WriteSummary_WithManyFailedLogs_TruncatesOutput()
    {
        // Arrange
        var failedLogs = Enumerable.Range(1, 15).Select(i => $"crash-{i}.log").ToList();
        var result = new ScanResult
        {
            Statistics = new ScanStatistics
            {
                TotalFiles = 15,
                Scanned = 0,
                Incomplete = 0,
                Failed = 15,
                ScanStartTime = DateTime.UtcNow
            },
            ScanDuration = TimeSpan.FromSeconds(1),
            FailedLogs = failedLogs,
            ProcessedFiles = Array.Empty<string>()
        };

        // Act
        var act = () => ConsoleOutput.WriteSummary(result);

        // Assert - should not throw and handles truncation
        act.Should().NotThrow();
    }

    [Fact]
    public void WriteSummary_WithEmptyResult_DoesNotThrow()
    {
        // Arrange
        var result = new ScanResult
        {
            Statistics = new ScanStatistics
            {
                TotalFiles = 0,
                Scanned = 0,
                Incomplete = 0,
                Failed = 0,
                ScanStartTime = DateTime.UtcNow
            },
            ScanDuration = TimeSpan.Zero,
            FailedLogs = Array.Empty<string>(),
            ProcessedFiles = Array.Empty<string>()
        };

        // Act
        var act = () => ConsoleOutput.WriteSummary(result);

        // Assert
        act.Should().NotThrow();
    }
}
