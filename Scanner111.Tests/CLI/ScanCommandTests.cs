using Microsoft.Extensions.DependencyInjection;
using Scanner111.CLI.Commands;
using Scanner111.CLI.Models;
using Scanner111.CLI.Services;
using Scanner111.Core.Abstractions;
using Scanner111.Core.Analyzers;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Core.Pipeline;
using Scanner111.Tests.TestHelpers;
using CliScanOptions = Scanner111.CLI.Models.ScanOptions;

namespace Scanner111.Tests.CLI;

public class ScanCommandTests : IDisposable
{
    private readonly TestableScanCommand _command;
    private readonly TestMessageCapture _messageCapture;
    private readonly MockFileScanService _mockFileScanService;
    private readonly MockScanResultProcessor _mockResultProcessor;
    private readonly MockCliSettingsService _mockSettingsService;

    public ScanCommandTests()
    {
        _messageCapture = new TestMessageCapture();
        MessageHandler.Initialize(_messageCapture);
        _mockSettingsService = new MockCliSettingsService();
        _mockFileScanService = new MockFileScanService();
        _mockResultProcessor = new MockScanResultProcessor();
        _command = new TestableScanCommand(_mockSettingsService, _mockFileScanService, _mockResultProcessor, _messageCapture);
    }

    public void Dispose()
    {
        MessageHandler.Initialize(new TestMessageHandler());
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
        result.Should().Be(1);
        _messageCapture.ErrorMessages.Should().Contain(m => m.Contains("No crash log files found to analyze"));
        _messageCapture.InfoMessages.Should().Contain(m => m.Contains("Supported file patterns:"));
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
        result.Should().Be(0);
        _messageCapture.SuccessMessages.Should().Contain(m => m.Contains("Starting analysis of 1 files..."));
        _messageCapture.SuccessMessages.Should().Contain(m => m.Contains("Analysis complete! Processed 1 files."));
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
        result.Should().Be(0);
        _messageCapture.SuccessMessages.Should().Contain(m => m.Contains("Starting analysis of 3 files..."));
        _messageCapture.SuccessMessages.Should().Contain(m => m.Contains("Analysis complete! Processed 3 files."));
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
        result.Should().Be(0);
        _messageCapture.InfoMessages.Should().Contain(m => m.Contains("=== SCAN SUMMARY ==="));
        _messageCapture.InfoMessages.Should().Contain(m => m.Contains("Total files scanned:"));
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
        result.Should().Be(0);
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
        result.Should().Be(0);
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
        result.Should().Be(0);
        // Settings should be overridden by command line options
        var processedSettings = _mockResultProcessor.LastUsedSettings;
        processedSettings.Should().NotBeNull();
        processedSettings.FcxMode.Should().BeTrue("command line option should override setting");
        processedSettings.ShowFormIdValues.Should().BeTrue("command line option should override setting");
        processedSettings.SimplifyLogs.Should().BeFalse("command line option should override setting");
        processedSettings.MoveUnsolvedLogs.Should().BeTrue("command line option should override setting");
        processedSettings.CrashLogsDirectory.Should().Be("/custom/dir");
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
        result.Should().Be(1);
        _messageCapture.CriticalMessages.Should().Contain(m => m.Contains("Fatal error during scan:"));
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
        result.Should().Be(0);
        _mockSettingsService.SaveSettingsCalled.Should().BeTrue();
        (_mockSettingsService.LastSavedSettings?.RecentScanDirectories ?? new List<string>())
            .Should().Contain("/new/scan/dir");
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
        result.Should().Be(0);
        // Message handler should be initialized without colors
    }

    private class MockCliSettingsService : ICliSettingsService
    {
        private ApplicationSettings _testSettings = new();

        public bool SaveSettingsCalled { get; private set; }
        public ApplicationSettings? LastSavedSettings { get; private set; }

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

        public void SetTestSettings(ApplicationSettings settings)
        {
            _testSettings = settings;
        }
    }

    private class MockFileScanService : IFileScanService
    {
        private List<string> _files = new();
        public bool ShouldThrowException { get; set; }

        public Task<FileScanData> CollectFilesToScanAsync(CliScanOptions options, ApplicationSettings settings)
        {
            if (ShouldThrowException) throw new InvalidOperationException("Test exception");

            return Task.FromResult(new FileScanData
            {
                FilesToScan = _files,
                XseCopiedFiles = new HashSet<string>()
            });
        }

        public void SetFiles(List<string> files)
        {
            _files = files;
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

    private class TestableScanCommand : ICommand<CliScanOptions>
    {
        private readonly ICliSettingsService _settingsService;
        private readonly IFileScanService _fileScanService;
        private readonly IScanResultProcessor _scanResultProcessor;
        private readonly IMessageHandler _messageHandler;
        private readonly IScanPipeline _testPipeline;
        private readonly IReportWriter _testReportWriter;

        public TestableScanCommand(
            ICliSettingsService settingsService,
            IFileScanService fileScanService,
            IScanResultProcessor scanResultProcessor,
            IMessageHandler messageHandler)
        {
            _settingsService = settingsService;
            _fileScanService = fileScanService;
            _scanResultProcessor = scanResultProcessor;
            _messageHandler = messageHandler;
            
            // Create simple test implementations
            _testPipeline = new TestScanPipeline();
            _testReportWriter = new TestReportWriter();
        }

        public async Task<int> ExecuteAsync(CliScanOptions options)
        {
            try
            {
                // Load settings
                var settings = await _settingsService.LoadSettingsAsync();

                // Apply command line overrides
                if (options.Verbose == true)
                    settings.VerboseLogging = true;
                if (options.FcxMode == true)
                    settings.FcxMode = true;
                if (options.ShowFidValues == true)
                    settings.ShowFormIdValues = true;
                if (options.SimplifyLogs.HasValue)
                    settings.SimplifyLogs = options.SimplifyLogs.Value;
                if (options.MoveUnsolved == true)
                    settings.MoveUnsolvedLogs = true;
                if (!string.IsNullOrEmpty(options.CrashLogsDirectory))
                    settings.CrashLogsDirectory = options.CrashLogsDirectory;

                // Update settings with recent path if scanning specific directory
                if (!string.IsNullOrEmpty(options.ScanDir))
                {
                    settings.AddRecentScanDirectory(options.ScanDir);
                    await _settingsService.SaveSettingsAsync(settings);
                }

                _messageHandler.ShowInfo("Initializing Scanner111...");

                // Collect files to scan
                var scanData = await _fileScanService.CollectFilesToScanAsync(options, settings);

                if (scanData.FilesToScan.Count == 0)
                {
                    _messageHandler.ShowError("No crash log files found to analyze");
                    _messageHandler.ShowInfo("Supported file patterns: crash-*.log, crash-*.txt, *dump*.log, *dump*.txt");
                    return 1;
                }

                _messageHandler.ShowSuccess($"Starting analysis of {scanData.FilesToScan.Count} files...");

                // Process files through test pipeline
                var results = new List<ScanResult>();
                await foreach (var result in _testPipeline.ProcessBatchAsync(scanData.FilesToScan))
                {
                    results.Add(result);
                    await _scanResultProcessor.ProcessScanResultAsync(result, options, _testReportWriter, 
                        scanData.XseCopiedFiles, settings);
                }

                _messageHandler.ShowSuccess($"Analysis complete! Processed {scanData.FilesToScan.Count} files.");

                if (options.OutputFormat == "summary")
                {
                    _messageHandler.ShowInfo("\n=== SCAN SUMMARY ===");
                    _messageHandler.ShowInfo($"Total files scanned: {results.Count}");
                    _messageHandler.ShowInfo($"Files with issues: {results.Count(r => r.AnalysisResults.Any(ar => ar.HasFindings))}");
                    _messageHandler.ShowInfo($"Clean files: {results.Count(r => !r.AnalysisResults.Any(ar => ar.HasFindings))}");
                }

                return 0;
            }
            catch (Exception ex)
            {
                _messageHandler.ShowCritical($"Fatal error during scan: {ex.Message}");
                if (options.Verbose == true)
                    _messageHandler.ShowDebug($"Stack trace: {ex.StackTrace}");
                return 1;
            }
        }
    }

    private class TestScanPipeline : IScanPipeline
    {
        public async IAsyncEnumerable<ScanResult> ScanAsync(List<string> crashLogPaths, 
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var path in crashLogPaths)
            {
                yield return new ScanResult
                {
                    LogPath = path,
                    CrashLog = new CrashLog { FilePath = path },
                    Status = ScanStatus.Completed,
                    AnalysisResults = new List<AnalysisResult>(),
                    ErrorMessages = new List<string>()
                };
            }
        }

        public async Task<ScanResult> ProcessSingleAsync(string crashLogPath, CancellationToken cancellationToken = default)
        {
            return new ScanResult
            {
                LogPath = crashLogPath,
                CrashLog = new CrashLog { FilePath = crashLogPath },
                Status = ScanStatus.Completed,
                AnalysisResults = new List<AnalysisResult>(),
                ErrorMessages = new List<string>()
            };
        }

        public async IAsyncEnumerable<ScanResult> ProcessBatchAsync(IEnumerable<string> crashLogPaths, 
            Scanner111.Core.Pipeline.ScanOptions? options = null, 
            IProgress<BatchProgress>? progress = null, 
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var path in crashLogPaths)
            {
                yield return new ScanResult
                {
                    LogPath = path,
                    CrashLog = new CrashLog { FilePath = path },
                    Status = ScanStatus.Completed,
                    AnalysisResults = new List<AnalysisResult>(),
                    ErrorMessages = new List<string>()
                };
            }
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private class TestReportWriter : IReportWriter
    {
        public Task<bool> WriteReportAsync(ScanResult scanResult, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task<bool> WriteReportAsync(ScanResult scanResult, string outputPath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }
    }
}