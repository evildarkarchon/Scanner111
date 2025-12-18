using FluentAssertions;
using Scanner111.Cli.Progress;
using Scanner111.Common.Models.Configuration;
using Scanner111.Common.Services.Orchestration;

namespace Scanner111.Cli.Tests.Progress;

public class ConsoleProgressReporterTests
{
    [Fact]
    public void Report_WithValidProgress_DoesNotThrow()
    {
        // Arrange
        var reporter = new ConsoleProgressReporter();
        var progress = new ScanProgress
        {
            FilesProcessed = 5,
            TotalFiles = 10,
            CurrentFile = "crash-test.log",
            Statistics = new ScanStatistics
            {
                TotalFiles = 10,
                Scanned = 5,
                ScanStartTime = DateTime.UtcNow
            }
        };

        // Act
        var act = () => reporter.Report(progress);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Report_AtCompletion_DoesNotThrow()
    {
        // Arrange
        var reporter = new ConsoleProgressReporter();
        var progress = new ScanProgress
        {
            FilesProcessed = 10,
            TotalFiles = 10,
            Statistics = new ScanStatistics
            {
                TotalFiles = 10,
                Scanned = 10,
                ScanStartTime = DateTime.UtcNow
            }
        };

        // Act
        var act = () => reporter.Report(progress);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Report_WithZeroFiles_DoesNotThrow()
    {
        // Arrange
        var reporter = new ConsoleProgressReporter();
        var progress = new ScanProgress
        {
            FilesProcessed = 0,
            TotalFiles = 0,
            Statistics = new ScanStatistics
            {
                TotalFiles = 0,
                Scanned = 0,
                ScanStartTime = DateTime.UtcNow
            }
        };

        // Act
        var act = () => reporter.Report(progress);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Report_MultipleUpdates_HandlesThrottling()
    {
        // Arrange
        var reporter = new ConsoleProgressReporter();

        // Act - rapid updates should not throw
        var act = () =>
        {
            for (var i = 0; i < 100; i++)
            {
                reporter.Report(new ScanProgress
                {
                    FilesProcessed = i,
                    TotalFiles = 100,
                    Statistics = new ScanStatistics
                    {
                        TotalFiles = 100,
                        Scanned = i,
                        ScanStartTime = DateTime.UtcNow
                    }
                });
            }
        };

        // Assert
        act.Should().NotThrow();
    }
}
