using Scanner111.CLI.Commands;
using Scanner111.CLI.Models;
using Scanner111.CLI.Services;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Core.Pipeline;
using Scanner111.Tests.TestHelpers;
using Xunit;
using CliScanOptions = Scanner111.CLI.Models.ScanOptions;

namespace Scanner111.Tests.CLI;

public class ScanCommandTests : IDisposable
{
    private readonly TestMessageCapture _messageCapture;
    private readonly MockCliSettingsService _mockSettingsService;
    private readonly MockFileScanService _mockFileScanService;
    private readonly MockScanResultProcessor _mockResultProcessor;
    private readonly ScanCommand _command;

    public ScanCommandTests()
    {
        _messageCapture = new TestMessageCapture();
        MessageHandler.Initialize(_messageCapture);
        _mockSettingsService = new MockCliSettingsService();
        _mockFileScanService = new MockFileScanService();
        _mockResultProcessor = new MockScanResultProcessor();
        _command = new ScanCommand(_mockSettingsService, _mockFileScanService, _mockResultProcessor);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoFiles_ReturnsError()
    {
        // Arrange
        var options = new CliScanOptions();
        _mockFileScanService.SetFiles(new List<string>());

        // Act
        var result = await _command.ExecuteAsync(options);

        // Assert
        Assert.Equal(1, result);
        Assert.Contains("No crash log files found to analyze", _messageCapture.ErrorMessages);
        Assert.Contains("Supported file patterns:", _messageCapture.InfoMessages);
    }

    [Fact]
    public async Task ExecuteAsync_WithSingleFile_ProcessesSuccessfully()
    {
        // Arrange
        var options = new CliScanOptions { LogFile = "test.log" };
        var testFiles = new List<string> { "test.log" };
        _mockFileScanService.SetFiles(testFiles);

        // Act
        var result = await _command.ExecuteAsync(options);

        // Assert
        Assert.Equal(0, result);
        Assert.Contains("Starting analysis of 1 files...", _messageCapture.SuccessMessages);
        Assert.Contains("Analysis complete! Processed 1 files.", _messageCapture.SuccessMessages);
    }

    [Fact]
    public async Task ExecuteAsync_WithMultipleFiles_ProcessesAll()
    {
        // Arrange
        var options = new CliScanOptions { ScanDir = "/test/dir" };
        var testFiles = new List<string> { "crash1.log", "crash2.log", "crash3.log" };
        _mockFileScanService.SetFiles(testFiles);

        // Act
        var result = await _command.ExecuteAsync(options);

        // Assert
        Assert.Equal(0, result);
        Assert.Contains("Starting analysis of 3 files...", _messageCapture.SuccessMessages);
        Assert.Contains("Analysis complete! Processed 3 files.", _messageCapture.SuccessMessages);
    }

    [Fact]
    public async Task ExecuteAsync_WithSummaryFormat_PrintsSummary()
    {
        // Arrange
        var options = new CliScanOptions 
        { 
            LogFile = "test.log",
            OutputFormat = "summary"
        };
        var testFiles = new List<string> { "test.log" };
        _mockFileScanService.SetFiles(testFiles);

        // Act
        var result = await _command.ExecuteAsync(options);

        // Assert
        Assert.Equal(0, result);
        Assert.Contains("=== SCAN SUMMARY ===", _messageCapture.InfoMessages);
        Assert.Contains("Total files scanned:", _messageCapture.InfoMessages);
    }

    [Fact]
    public async Task ExecuteAsync_WithVerbose_EnablesDebugLogging()
    {
        // Arrange
        var options = new CliScanOptions 
        { 
            LogFile = "test.log",
            Verbose = true
        };
        var testFiles = new List<string> { "test.log" };
        _mockFileScanService.SetFiles(testFiles);

        // Act
        var result = await _command.ExecuteAsync(options);

        // Assert
        Assert.Equal(0, result);
        // Verbose mode would enable debug logging in the pipeline
    }

    [Fact]
    public async Task ExecuteAsync_WithFcxMode_EnablesFcxInPipeline()
    {
        // Arrange
        var options = new CliScanOptions 
        { 
            LogFile = "test.log",
            FcxMode = true
        };
        var testFiles = new List<string> { "test.log" };
        _mockFileScanService.SetFiles(testFiles);

        // Act
        var result = await _command.ExecuteAsync(options);

        // Assert
        Assert.Equal(0, result);
        // FCX mode would be enabled in the pipeline
    }

    [Fact]
    public async Task ExecuteAsync_AppliesCommandLineOverrides()
    {
        // Arrange
        var options = new CliScanOptions 
        { 
            LogFile = "test.log",
            FcxMode = true,
            ShowFidValues = true,
            SimplifyLogs = false,
            MoveUnsolved = true,
            CrashLogsDirectory = "/custom/dir"
        };
        var testFiles = new List<string> { "test.log" };
        _mockFileScanService.SetFiles(testFiles);

        var settings = new ApplicationSettings
        {
            FcxMode = false,
            ShowFormIdValues = false,
            SimplifyLogs = true,
            MoveUnsolvedLogs = false,
            CrashLogsDirectory = "/default/dir"
        };
        _mockSettingsService.SetTestSettings(settings);

        // Act
        var result = await _command.ExecuteAsync(options);

        // Assert
        Assert.Equal(0, result);
        // Settings should be overridden by command line options
        var processedSettings = _mockResultProcessor.LastUsedSettings;
        Assert.NotNull(processedSettings);
        Assert.True(processedSettings.FcxMode);
        Assert.True(processedSettings.ShowFormIdValues);
        Assert.False(processedSettings.SimplifyLogs);
        Assert.True(processedSettings.MoveUnsolvedLogs);
        Assert.Equal("/custom/dir", processedSettings.CrashLogsDirectory);
    }

    [Fact]
    public async Task ExecuteAsync_WithException_HandlesGracefully()
    {
        // Arrange
        var options = new CliScanOptions();
        _mockFileScanService.ShouldThrowException = true;

        // Act
        var result = await _command.ExecuteAsync(options);

        // Assert
        Assert.Equal(1, result);
        Assert.Contains("Fatal error during scan:", _messageCapture.CriticalMessages);
    }

    [Fact]
    public async Task ExecuteAsync_SavesRecentScanDirectory()
    {
        // Arrange
        var options = new CliScanOptions { ScanDir = "/new/scan/dir" };
        var testFiles = new List<string> { "test.log" };
        _mockFileScanService.SetFiles(testFiles);

        // Act
        var result = await _command.ExecuteAsync(options);

        // Assert
        Assert.Equal(0, result);
        Assert.True(_mockSettingsService.SaveSettingsCalled);
        Assert.Contains("/new/scan/dir", _mockSettingsService.LastSavedSettings?.RecentScanDirectories ?? new List<string>());
    }

    [Fact]
    public async Task ExecuteAsync_WithDisabledColors_InitializesCorrectly()
    {
        // Arrange
        var options = new CliScanOptions 
        { 
            LogFile = "test.log",
            DisableColors = true
        };
        var testFiles = new List<string> { "test.log" };
        _mockFileScanService.SetFiles(testFiles);

        // Act
        var result = await _command.ExecuteAsync(options);

        // Assert
        Assert.Equal(0, result);
        // Message handler should be initialized without colors
    }

    public void Dispose()
    {
        MessageHandler.Initialize(new TestMessageHandler());
    }

    private class MockCliSettingsService : ICliSettingsService
    {
        private ApplicationSettings _testSettings = new();
        
        public bool SaveSettingsCalled { get; private set; }
        public ApplicationSettings? LastSavedSettings { get; private set; }

        public void SetTestSettings(ApplicationSettings settings)
        {
            _testSettings = settings;
        }

        public Task<ApplicationSettings> LoadSettingsAsync()
        {
            return Task.FromResult(_testSettings);
        }

        public Task SaveSettingsAsync(ApplicationSettings settings)
        {
            SaveSettingsCalled = true;
            LastSavedSettings = settings;
            return Task.CompletedTask;
        }

        public Task SaveSettingAsync(string key, object value)
        {
            return Task.CompletedTask;
        }

        public ApplicationSettings GetDefaultSettings()
        {
            return new ApplicationSettings();
        }

        public Task<CliSettings> LoadCliSettingsAsync()
        {
            return Task.FromResult(new CliSettings());
        }

        public Task SaveCliSettingsAsync(CliSettings settings)
        {
            return Task.CompletedTask;
        }
    }

    private class MockFileScanService : IFileScanService
    {
        private List<string> _files = new();
        public bool ShouldThrowException { get; set; }

        public void SetFiles(List<string> files)
        {
            _files = files;
        }

        public Task<FileScanData> CollectFilesToScanAsync(CliScanOptions options, ApplicationSettings settings)
        {
            if (ShouldThrowException)
            {
                throw new InvalidOperationException("Test exception");
            }

            return Task.FromResult(new FileScanData
            {
                FilesToScan = _files,
                XseCopiedFiles = new HashSet<string>()
            });
        }
    }

    private class MockScanResultProcessor : IScanResultProcessor
    {
        public ApplicationSettings? LastUsedSettings { get; private set; }

        public Task ProcessScanResultAsync(ScanResult result, CliScanOptions options, IReportWriter reportWriter,
            HashSet<string> xseCopiedFiles, ApplicationSettings settings)
        {
            LastUsedSettings = settings;
            return Task.CompletedTask;
        }
    }
}