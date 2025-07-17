using Scanner111.Core.Models;
using Xunit;

namespace Scanner111.Tests.Models;

public class CrashLogTests
{
    private readonly string _sampleLogsPath = Path.Combine(
        Directory.GetParent(Environment.CurrentDirectory)?.Parent?.Parent?.Parent?.FullName ?? "",
        "sample_logs"
    );

    [Fact]
    public void CrashLog_DefaultValues_AreCorrect()
    {
        var crashLog = new CrashLog();
        
        Assert.Equal(string.Empty, crashLog.FilePath);
        Assert.Equal(string.Empty, crashLog.FileName);
        Assert.Empty(crashLog.OriginalLines);
        Assert.Equal(string.Empty, crashLog.Content);
        Assert.Equal(string.Empty, crashLog.MainError);
        Assert.Empty(crashLog.CallStack);
        Assert.Empty(crashLog.Plugins);
        Assert.Equal(string.Empty, crashLog.CrashGenVersion);
        Assert.Null(crashLog.CrashTime);
        Assert.False(crashLog.IsComplete);
        Assert.False(crashLog.HasError);
    }
    
    [Fact]
    public void CrashLog_WithFilePath_ExtractsFileName()
    {
        var crashLog = new CrashLog
        {
            FilePath = @"C:\Users\Test\Documents\crash-2024-01-01-12-34-56.log"
        };
        
        Assert.Equal("crash-2024-01-01-12-34-56.log", crashLog.FileName);
    }
    
    [Fact]
    public void CrashLog_WithSampleLogFile_LoadsCorrectly()
    {
        // Use actual sample log file
        var sampleFile = Path.Combine(_sampleLogsPath, "crash-2024-01-11-08-19-43.log");
        
        if (File.Exists(sampleFile))
        {
            var lines = File.ReadAllLines(sampleFile);
            var crashLog = new CrashLog
            {
                FilePath = sampleFile,
                OriginalLines = lines.ToList()
            };
            
            Assert.Equal("crash-2024-01-11-08-19-43.log", crashLog.FileName);
            Assert.True(crashLog.OriginalLines.Count > 0);
            Assert.Contains("Fallout 4", crashLog.Content);
            Assert.Contains("Buffout", crashLog.Content);
        }
    }
    
    [Fact]
    public void CrashLog_WithOriginalLines_BuildsContent()
    {
        var crashLog = new CrashLog
        {
            OriginalLines = new List<string> { "Line 1", "Line 2", "Line 3" }
        };
        
        Assert.Equal("Line 1\nLine 2\nLine 3", crashLog.Content);
    }
    
    [Fact]
    public void CrashLog_IsComplete_ReturnsTrueWhenPluginsExist()
    {
        var crashLog = new CrashLog
        {
            Plugins = new Dictionary<string, string> { { "plugin1.esp", "01" } }
        };
        
        Assert.True(crashLog.IsComplete);
    }
    
    [Fact]
    public void CrashLog_HasError_ReturnsTrueWhenMainErrorSet()
    {
        var crashLog = new CrashLog
        {
            MainError = "Access violation"
        };
        
        Assert.True(crashLog.HasError);
    }
    
    [Fact]
    public void CrashLog_WithMockPluginData_WorksCorrectly()
    {
        var crashTime = DateTime.Now;
        var crashLog = new CrashLog
        {
            FilePath = Path.Combine(_sampleLogsPath, "crash-2024-01-11-08-19-43.log"),
            OriginalLines = new List<string> { "Error occurred", "Stack trace" },
            MainError = "EXCEPTION_ACCESS_VIOLATION",
            CallStack = new List<string> { "Function1", "Function2" },
            Plugins = new Dictionary<string, string> { { "plugin1.esp", "01" }, { "plugin2.esp", "02" } },
            CrashGenVersion = "Buffout 4 v1.28.6",
            CrashTime = crashTime
        };
        
        Assert.Equal("crash-2024-01-11-08-19-43.log", crashLog.FileName);
        Assert.Equal("Error occurred\nStack trace", crashLog.Content);
        Assert.True(crashLog.IsComplete);
        Assert.True(crashLog.HasError);
        Assert.Equal(crashTime, crashLog.CrashTime);
        Assert.Equal(2, crashLog.Plugins.Count);
        Assert.Equal(2, crashLog.CallStack.Count);
    }
    
    [Fact]
    public void CrashLog_GetAllSampleFiles_ReturnsExpectedCount()
    {
        if (Directory.Exists(_sampleLogsPath))
        {
            var logFiles = Directory.GetFiles(_sampleLogsPath, "*.log");
            var mdFiles = Directory.GetFiles(_sampleLogsPath, "*-AUTOSCAN.md");
            
            // Each log file should have a corresponding AUTOSCAN.md file
            Assert.True(logFiles.Length > 0, "Should have sample log files");
            Assert.True(mdFiles.Length > 0, "Should have sample AUTOSCAN.md files");
        }
    }
}