using Scanner111.Core.Models;
using Xunit;

namespace Scanner111.Tests.Models;

public class ScanResultTests
{
    private readonly string _sampleLogsPath = Path.Combine(
        Directory.GetParent(Environment.CurrentDirectory)?.Parent?.Parent?.Parent?.FullName ?? "",
        "sample_logs"
    );

    [Fact]
    public void ScanResult_DefaultValues_AreCorrect()
    {
        var result = new ScanResult { LogPath = "test.log" };
        
        Assert.Equal("test.log", result.LogPath);
        Assert.Empty(result.Report);
        Assert.False(result.Failed);
        Assert.NotNull(result.Statistics);
        Assert.Equal(string.Empty, result.ReportText);
        Assert.Equal("test-AUTOSCAN.md", result.OutputPath);
    }
    
    [Fact]
    public void ScanResult_WithReport_BuildsReportText()
    {
        var result = new ScanResult 
        { 
            LogPath = "test.log",
            Report = new List<string> { "Line 1\n", "Line 2\n", "Line 3\n" }
        };
        
        Assert.Equal("Line 1\nLine 2\nLine 3\n", result.ReportText);
    }
    
    [Fact]
    public void ScanResult_WithSampleLogPath_GeneratesCorrectOutputPath()
    {
        var sampleFile = Path.Combine(_sampleLogsPath, "crash-2024-01-11-08-19-43.log");
        var result = new ScanResult { LogPath = sampleFile };
        
        var expectedOutput = Path.Combine(_sampleLogsPath, "crash-2024-01-11-08-19-43-AUTOSCAN.md");
        Assert.Equal(expectedOutput, result.OutputPath);
    }
    
    [Fact]
    public void ScanStatistics_DefaultValues_AreCorrect()
    {
        var stats = new ScanStatistics();
        
        Assert.Equal(0, stats["scanned"]);
        Assert.Equal(0, stats["incomplete"]);
        Assert.Equal(0, stats["failed"]);
        Assert.Equal(0, stats.Scanned);
        Assert.Equal(0, stats.Incomplete);
        Assert.Equal(0, stats.Failed);
    }
    
    [Fact]
    public void ScanStatistics_Properties_WorkCorrectly()
    {
        var stats = new ScanStatistics
        {
            Scanned = 5,
            Incomplete = 2,
            Failed = 1
        };
        
        Assert.Equal(5, stats["scanned"]);
        Assert.Equal(2, stats["incomplete"]);
        Assert.Equal(1, stats["failed"]);
    }
    
    [Fact]
    public void ScanStatistics_Increment_WorksCorrectly()
    {
        var stats = new ScanStatistics();
        
        stats.Increment("scanned");
        stats.Increment("scanned");
        stats.Increment("failed");
        stats.Increment("custom");
        
        Assert.Equal(2, stats["scanned"]);
        Assert.Equal(1, stats["failed"]);
        Assert.Equal(1, stats["custom"]);
        Assert.Equal(0, stats["incomplete"]);
    }
    
    [Fact]
    public void ScanResult_WithAllProperties_WorksCorrectly()
    {
        var stats = new ScanStatistics();
        stats.Increment("scanned");
        stats.Increment("failed");
        
        var result = new ScanResult
        {
            LogPath = Path.Combine(_sampleLogsPath, "crash-2024-01-11-08-19-43.log"),
            Report = new List<string> { "Error analysis:\n", "- Issue found\n" },
            Statistics = stats
        };
        result.Status = ScanStatus.Failed;
        
        Assert.True(result.Failed);
        Assert.Equal("Error analysis:\n- Issue found\n", result.ReportText);
        Assert.Equal(1, result.Statistics.Scanned);
        Assert.Equal(1, result.Statistics.Failed);
        Assert.Contains("crash-2024-01-11-08-19-43-AUTOSCAN.md", result.OutputPath);
    }
}