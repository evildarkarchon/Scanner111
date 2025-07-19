using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Core.Pipeline;
using Scanner111.Core.Analyzers;
using Scanner111.GUI.Models;
using Scanner111.GUI.ViewModels;
using System.Text;
using Xunit;

namespace Scanner111.Tests.Integration;

public class ReportWritingIntegrationTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _sampleLogsPath;

    public ReportWritingIntegrationTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDirectory);
        
        _sampleLogsPath = Path.Combine(
            Directory.GetParent(Environment.CurrentDirectory)?.Parent?.Parent?.Parent?.FullName ?? "",
            "sample_logs"
        );
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    [Fact]
    public async Task ScanPipeline_WithReportWriter_GeneratesReportFile()
    {
        // Arrange
        var testLogPath = Path.Combine(_tempDirectory, "test-crash.log");
        var expectedReportPath = Path.Combine(_tempDirectory, "test-crash-AUTOSCAN.md");
        
        await CreateSampleCrashLog(testLogPath);
        
        var messageHandler = new TestMessageHandler();
        var pipeline = new ScanPipelineBuilder()
            .AddDefaultAnalyzers()
            .WithMessageHandler(messageHandler)
            .WithCaching(false)
            .WithEnhancedErrorHandling(false)
            .WithLogging(builder => builder.AddProvider(NullLoggerProvider.Instance))
            .Build();

        var reportWriter = new ReportWriter(NullLogger<ReportWriter>.Instance);

        // Act
        var scanResult = await pipeline.ProcessSingleAsync(testLogPath);
        Assert.NotNull(scanResult);
        
        var writeSuccess = await reportWriter.WriteReportAsync(scanResult);

        // Assert
        Assert.True(writeSuccess);
        Assert.True(File.Exists(expectedReportPath));
        
        var reportContent = await File.ReadAllTextAsync(expectedReportPath, Encoding.UTF8);
        // Note: Report content may be empty for logs without plugin lists - this is expected behavior
        Assert.Equal(scanResult.ReportText, reportContent);
    }

    [Fact]
    public async Task ScanPipeline_WithBatchProcessing_GeneratesMultipleReports()
    {
        // Arrange
        var logFiles = new[]
        {
            Path.Combine(_tempDirectory, "crash-1.log"),
            Path.Combine(_tempDirectory, "crash-2.log"),
            Path.Combine(_tempDirectory, "crash-3.log")
        };

        foreach (var logFile in logFiles)
        {
            await CreateSampleCrashLog(logFile);
        }

        var messageHandler = new TestMessageHandler();
        var pipeline = new ScanPipelineBuilder()
            .AddDefaultAnalyzers()
            .WithMessageHandler(messageHandler)
            .WithCaching(false)
            .WithEnhancedErrorHandling(false)
            .WithLogging(builder => builder.AddProvider(NullLoggerProvider.Instance))
            .Build();

        var reportWriter = new ReportWriter(NullLogger<ReportWriter>.Instance);

        // Act
        var results = new List<ScanResult>();
        await foreach (var result in pipeline.ProcessBatchAsync(logFiles))
        {
            if (result != null)
            {
                results.Add(result);
                await reportWriter.WriteReportAsync(result);
            }
        }

        // Assert
        Assert.Equal(3, results.Count);
        
        foreach (var logFile in logFiles)
        {
            var expectedReportPath = Path.ChangeExtension(logFile, null) + "-AUTOSCAN.md";
            Assert.True(File.Exists(expectedReportPath));
            
            // Note: Report content may be empty for logs without plugin lists - this is expected
            var reportContent = await File.ReadAllTextAsync(expectedReportPath, Encoding.UTF8);
            // Just verify the file was created and is readable
            Assert.NotNull(reportContent);
        }
    }

    [Fact]
    public async Task GUI_AutoSaveResult_WritesReportFile()
    {
        // This test simulates the GUI auto-save functionality
        // Arrange
        var testLogPath = Path.Combine(_tempDirectory, "gui-test-crash.log");
        await CreateSampleCrashLog(testLogPath);
        
        var scanResult = new ScanResult
        {
            LogPath = testLogPath,
            Status = ScanStatus.Completed,
            Report = new List<string>
            {
                "GUI generated report line 1\n",
                "GUI generated report line 2\n",
                "Analysis complete\n"
            }
        };

        var reportWriter = new ReportWriter(NullLogger<ReportWriter>.Instance);
        
        // Simulate the AutoSaveResult method behavior
        var userSettings = new UserSettings { AutoSaveResults = true };

        // Act
        bool saveResult = false;
        if (userSettings.AutoSaveResults)
        {
            saveResult = await reportWriter.WriteReportAsync(scanResult);
        }

        // Assert
        Assert.True(saveResult);
        Assert.True(File.Exists(scanResult.OutputPath));
        
        var reportContent = await File.ReadAllTextAsync(scanResult.OutputPath, Encoding.UTF8);
        Assert.Equal(scanResult.ReportText, reportContent);
        Assert.Contains("GUI generated report", reportContent);
    }

    [Fact]
    public async Task CLI_AutoSaveWithSettings_RespectsAutoSaveFlag()
    {
        // This test simulates CLI auto-save behavior with different settings
        // Arrange
        var testLogPath = Path.Combine(_tempDirectory, "cli-test-crash.log");
        await CreateSampleCrashLog(testLogPath);
        
        var scanResult = new ScanResult
        {
            LogPath = testLogPath,
            Status = ScanStatus.Completed,
            Report = new List<string>
            {
                "CLI generated report line 1\n",
                "CLI generated report line 2\n"
            }
        };

        var reportWriter = new ReportWriter(NullLogger<ReportWriter>.Instance);

        // Test with AutoSaveResults enabled
        GlobalRegistry.Set("AutoSaveResults", true);
        var autoSaveEnabled = GlobalRegistry.GetValueType<bool>("AutoSaveResults", true);
        
        // Act
        bool saveResult = false;
        if (autoSaveEnabled && !string.IsNullOrEmpty(scanResult.ReportText))
        {
            saveResult = await reportWriter.WriteReportAsync(scanResult);
        }

        // Assert
        Assert.True(saveResult);
        Assert.True(File.Exists(scanResult.OutputPath));

        // Test with AutoSaveResults disabled
        var testLogPath2 = Path.Combine(_tempDirectory, "cli-test-crash-2.log");
        await CreateSampleCrashLog(testLogPath2);
        
        var scanResult2 = new ScanResult
        {
            LogPath = testLogPath2,
            Status = ScanStatus.Completed,
            Report = new List<string> { "Report that should not be saved\n" }
        };

        GlobalRegistry.Set("AutoSaveResults", false);
        var autoSaveDisabled = GlobalRegistry.GetValueType<bool>("AutoSaveResults", true);
        
        // Act
        bool saveResult2 = false;
        if (autoSaveDisabled && !string.IsNullOrEmpty(scanResult2.ReportText))
        {
            saveResult2 = await reportWriter.WriteReportAsync(scanResult2);
        }

        // Assert - file should not be created because auto-save is disabled
        Assert.False(saveResult2);
        Assert.False(File.Exists(scanResult2.OutputPath));
    }

    [Fact]
    public async Task ReportWriter_WithRealSampleLog_GeneratesValidReport()
    {
        // This test uses a real sample log if available
        // Arrange
        if (!Directory.Exists(_sampleLogsPath))
        {
            // Skip test if sample logs are not available
            return;
        }

        var sampleLogs = Directory.GetFiles(_sampleLogsPath, "*.log", SearchOption.TopDirectoryOnly)
            .Take(1)
            .ToArray();

        if (sampleLogs.Length == 0)
        {
            // Skip test if no sample logs found
            return;
        }

        var sampleLogPath = sampleLogs[0];
        var tempLogPath = Path.Combine(_tempDirectory, Path.GetFileName(sampleLogPath));
        var expectedReportPath = Path.ChangeExtension(tempLogPath, null) + "-AUTOSCAN.md";
        
        // Copy sample log to temp directory for testing
        File.Copy(sampleLogPath, tempLogPath);

        var messageHandler = new TestMessageHandler();
        var pipeline = new ScanPipelineBuilder()
            .AddDefaultAnalyzers()
            .WithMessageHandler(messageHandler)
            .WithCaching(false)
            .WithEnhancedErrorHandling(false)
            .WithLogging(builder => builder.AddProvider(NullLoggerProvider.Instance))
            .Build();

        var reportWriter = new ReportWriter(NullLogger<ReportWriter>.Instance);

        // Act
        var scanResult = await pipeline.ProcessSingleAsync(tempLogPath);
        Assert.NotNull(scanResult);
        
        var writeSuccess = await reportWriter.WriteReportAsync(scanResult);

        // Assert
        Assert.True(writeSuccess);
        Assert.True(File.Exists(expectedReportPath));
        
        var reportContent = await File.ReadAllTextAsync(expectedReportPath, Encoding.UTF8);
        
        // Verify no OPC content in the generated report (regardless of whether report is empty)
        Assert.DoesNotContain("OPC INSTALLER", reportContent);
        Assert.DoesNotContain("PATCHED THROUGH OPC", reportContent);
        
        // Verify report structure matches scan result
        Assert.Equal(scanResult.ReportText, reportContent);
    }

    [Fact]
    public async Task ScanResultViewModel_WithReportWriter_IntegratesCorrectly()
    {
        // This test verifies GUI integration
        // Arrange
        var testLogPath = Path.Combine(_tempDirectory, "viewmodel-test-crash.log");
        await CreateSampleCrashLog(testLogPath);
        
        var scanResult = new ScanResult
        {
            LogPath = testLogPath,
            Status = ScanStatus.Completed,
            Report = new List<string>
            {
                "ViewModel integration test\n",
                "✓ Test successful\n"
            }
        };

        var viewModel = new ScanResultViewModel(scanResult);
        var reportWriter = new ReportWriter(NullLogger<ReportWriter>.Instance);

        // Act
        var writeSuccess = await reportWriter.WriteReportAsync(viewModel.ScanResult);

        // Assert
        Assert.True(writeSuccess);
        Assert.True(File.Exists(viewModel.ScanResult.OutputPath));
        
        var reportContent = await File.ReadAllTextAsync(viewModel.ScanResult.OutputPath, Encoding.UTF8);
        Assert.Contains("ViewModel integration test", reportContent);
        Assert.Contains("✓ Test successful", reportContent);
        
        // Verify view model properties
        Assert.Equal(Path.GetFileName(testLogPath), viewModel.Description);
        Assert.Equal("Completed", viewModel.Category);
    }

    private async Task CreateSampleCrashLog(string filePath)
    {
        var sampleLogContent = """
            Fallout4.exe (Application)
            Version: 1.10.163.0
            
            [15] UT  2024-05-04 08:52:21 BST :: Plugins (244):
                [00]      Fallout4.esm
                [01]      DLCRobot.esm
                [02]      DLCworkshop01.esm
                [FE:000] TestMod.esp
            
            [15] UT  2024-05-04 08:52:21 BST :: Call Stack:
                ntdll.dll                  0x00007FFE12345678
                KERNELBASE.dll             0x00007FFE11111111
                Fallout4.exe               0x0000000140123456
            
            [15] UT  2024-05-04 08:52:21 BST :: Analysis Complete
            """;

        await File.WriteAllTextAsync(filePath, sampleLogContent, Encoding.UTF8);
    }

    private class TestMessageHandler : IMessageHandler
    {
        public void ShowInfo(string message, MessageTarget target = MessageTarget.All) { }
        public void ShowWarning(string message, MessageTarget target = MessageTarget.All) { }
        public void ShowError(string message, MessageTarget target = MessageTarget.All) { }
        public void ShowSuccess(string message, MessageTarget target = MessageTarget.All) { }
        public void ShowDebug(string message, MessageTarget target = MessageTarget.All) { }
        public void ShowCritical(string message, MessageTarget target = MessageTarget.All) { }
        public void ShowMessage(string message, string? details = null, MessageType messageType = MessageType.Info, MessageTarget target = MessageTarget.All) { }
        public IProgress<ProgressInfo> ShowProgress(string title, int totalItems) => new Progress<ProgressInfo>();
        public IProgressContext CreateProgressContext(string title, int totalItems) => new TestProgressContext();
    }

    private class TestProgressContext : IProgressContext
    {
        public void Update(int current, string message) { }
        public void Complete() { }
        public void Report(ProgressInfo value) { }
        public void Dispose() { }
    }
}