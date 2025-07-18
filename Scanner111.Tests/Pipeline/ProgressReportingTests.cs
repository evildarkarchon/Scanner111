using Xunit;
using Scanner111.Core.Pipeline;

namespace Scanner111.Tests.Pipeline;

public class ProgressReportingTests
{
    [Fact]
    public void DetailedProgressInfo_CalculatesProgressPercentageCorrectly()
    {
        // Arrange
        var progress = new DetailedProgressInfo
        {
            TotalFiles = 100,
            ProcessedFiles = 25
        };

        // Act & Assert
        Assert.Equal(25.0, progress.ProgressPercentage);
    }

    [Fact]
    public void DetailedProgressInfo_HandlesZeroTotalFiles()
    {
        // Arrange
        var progress = new DetailedProgressInfo
        {
            TotalFiles = 0,
            ProcessedFiles = 5
        };

        // Act & Assert
        Assert.Equal(0.0, progress.ProgressPercentage);
    }

    [Fact]
    public void DetailedProgressInfo_CalculatesPerformanceMetrics()
    {
        // Arrange
        var startTime = DateTime.UtcNow.AddMinutes(-2);
        var progress = new DetailedProgressInfo
        {
            StartTime = startTime,
            LastUpdateTime = DateTime.UtcNow,
            ProcessedFiles = 60
        };

        // Act & Assert
        Assert.True(progress.ElapsedTime.TotalMinutes >= 1.9);
        Assert.True(progress.FilesPerSecond > 0);
        Assert.True(progress.AverageFileTime > 0);
    }

    [Fact]
    public async Task DetailedProgress_ReportsFileStart()
    {
        // Arrange
        var progress = new DetailedProgress();
        DetailedProgressInfo? reportedProgress = null;
        var innerProgress = new Progress<DetailedProgressInfo>(p => reportedProgress = p);
        var detailedProgress = new DetailedProgress(innerProgress);

        // Act
        detailedProgress.ReportFileStart("test.log");

        // Wait a small amount for async reporting
        await Task.Delay(10);

        // Assert
        Assert.NotNull(reportedProgress);
        Assert.Equal("test.log", reportedProgress.CurrentFile);
        Assert.Equal(FileProcessingStatus.InProgress, reportedProgress.CurrentFileStatus);
    }

    [Fact]
    public void DetailedProgress_ReportsFileComplete()
    {
        // Arrange
        var progress = new DetailedProgress();
        DetailedProgressInfo? reportedProgress = null;
        var innerProgress = new Progress<DetailedProgressInfo>(p => reportedProgress = p);
        var detailedProgress = new DetailedProgress(innerProgress);

        // Act
        detailedProgress.ReportFileComplete("test.log", true);

        // Assert
        Assert.NotNull(reportedProgress);
        Assert.Equal("test.log", reportedProgress.CurrentFile);
        Assert.Equal(FileProcessingStatus.Completed, reportedProgress.CurrentFileStatus);
        Assert.Equal(1, reportedProgress.ProcessedFiles);
        Assert.Equal(1, reportedProgress.SuccessfulFiles);
    }

    [Fact]
    public void DetailedProgress_ReportsFileFailure()
    {
        // Arrange
        var progress = new DetailedProgress();
        DetailedProgressInfo? reportedProgress = null;
        var innerProgress = new Progress<DetailedProgressInfo>(p => reportedProgress = p);
        var detailedProgress = new DetailedProgress(innerProgress);

        // Act
        detailedProgress.ReportFileComplete("test.log", false);

        // Assert
        Assert.NotNull(reportedProgress);
        Assert.Equal("test.log", reportedProgress.CurrentFile);
        Assert.Equal(FileProcessingStatus.Failed, reportedProgress.CurrentFileStatus);
        Assert.Equal(1, reportedProgress.ProcessedFiles);
        Assert.Equal(1, reportedProgress.FailedFiles);
    }

    [Fact]
    public async Task DetailedProgress_TracksAnalyzerProgress()
    {
        // Arrange
        var progress = new DetailedProgress();
        DetailedProgressInfo? reportedProgress = null;
        var innerProgress = new Progress<DetailedProgressInfo>(p => reportedProgress = p);
        var detailedProgress = new DetailedProgress(innerProgress);

        // Act
        detailedProgress.ReportAnalyzerStart("FormIdAnalyzer", "test.log");

        // Wait for async reporting
        await Task.Delay(10);

        // Assert
        Assert.NotNull(reportedProgress);
        Assert.Single(reportedProgress.ActiveAnalyzers);
        
        var analyzer = reportedProgress.ActiveAnalyzers.First();
        Assert.Equal("FormIdAnalyzer", analyzer.AnalyzerName);
        Assert.Equal("test.log", analyzer.FileName);
        Assert.Equal(AnalyzerStatus.Running, analyzer.Status);
    }

    [Fact]
    public void DetailedProgress_CompletesAnalyzerProgress()
    {
        // Arrange
        var progress = new DetailedProgress();
        DetailedProgressInfo? reportedProgress = null;
        var innerProgress = new Progress<DetailedProgressInfo>(p => reportedProgress = p);
        var detailedProgress = new DetailedProgress(innerProgress);

        // Act
        detailedProgress.ReportAnalyzerStart("FormIdAnalyzer", "test.log");
        detailedProgress.ReportAnalyzerComplete("FormIdAnalyzer", "test.log", true);

        // Assert
        Assert.NotNull(reportedProgress);
        Assert.Empty(reportedProgress.ActiveAnalyzers); // Should be removed from active list
    }

    [Fact]
    public void AnalyzerProgress_CalculatesDuration()
    {
        // Arrange
        var startTime = DateTime.UtcNow;
        var progress = new AnalyzerProgress
        {
            AnalyzerName = "TestAnalyzer",
            FileName = "test.log",
            Status = AnalyzerStatus.Running,
            StartTime = startTime
        };

        // Act
        Thread.Sleep(10); // Small delay to ensure time difference
        progress.EndTime = DateTime.UtcNow;
        progress.Duration = progress.EndTime - progress.StartTime;
        progress.Status = AnalyzerStatus.Completed;

        // Assert
        Assert.True(progress.Duration?.TotalMilliseconds > 0);
        Assert.Equal(AnalyzerStatus.Completed, progress.Status);
    }

    [Theory]
    [InlineData(FileProcessingStatus.Pending)]
    [InlineData(FileProcessingStatus.InProgress)]
    [InlineData(FileProcessingStatus.Completed)]
    [InlineData(FileProcessingStatus.Failed)]
    [InlineData(FileProcessingStatus.Cancelled)]
    public void FileProcessingStatus_AllValuesSupported(FileProcessingStatus status)
    {
        // Arrange & Act
        var progress = new DetailedProgressInfo
        {
            CurrentFileStatus = status
        };

        // Assert
        Assert.Equal(status, progress.CurrentFileStatus);
    }

    [Theory]
    [InlineData(AnalyzerStatus.Pending)]
    [InlineData(AnalyzerStatus.Running)]
    [InlineData(AnalyzerStatus.Completed)]
    [InlineData(AnalyzerStatus.Failed)]
    [InlineData(AnalyzerStatus.Skipped)]
    public void AnalyzerStatus_AllValuesSupported(AnalyzerStatus status)
    {
        // Arrange & Act
        var progress = new AnalyzerProgress
        {
            Status = status
        };

        // Assert
        Assert.Equal(status, progress.Status);
    }
}