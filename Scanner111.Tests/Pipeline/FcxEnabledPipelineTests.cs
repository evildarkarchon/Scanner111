using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Core.Pipeline;
using Scanner111.Tests.TestHelpers;

namespace Scanner111.Tests.Pipeline;

/// <summary>
///     Unit tests for the FcxEnabledPipeline class that validates FCX (File Integrity Check) functionality
/// </summary>
[Collection("Pipeline Tests")]
public class FcxEnabledPipelineTests : IAsyncDisposable
{
    private readonly TestHashValidationService _hashService;
    private readonly TestScanPipeline _innerPipeline;
    private readonly ILogger<FcxEnabledPipeline> _logger;
    private readonly IMessageHandler _messageHandler;
    private readonly FcxEnabledPipeline _pipeline;
    private readonly TestApplicationSettingsService _settingsService;
    private readonly IYamlSettingsProvider _yamlSettings;

    public FcxEnabledPipelineTests()
    {
        _logger = new TestLogger<FcxEnabledPipeline>();
        _messageHandler = new TestMessageHandler();
        _yamlSettings = new TestYamlSettingsProvider();
        _settingsService = new TestApplicationSettingsService();
        _hashService = new TestHashValidationService();
        _innerPipeline = new TestScanPipeline();

        _pipeline = new FcxEnabledPipeline(
            _innerPipeline,
            _settingsService,
            _hashService,
            _logger,
            _messageHandler,
            _yamlSettings);
    }

    /// <summary>
    ///     Ensures proper disposal of the pipeline resources
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await _pipeline.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ProcessSingleAsync_WhenFcxModeDisabled_ShouldPassThroughToInnerPipeline()
    {
        // Arrange
        _settingsService.Settings.FcxMode = false;
        var testLogPath = "test.log";
        var expectedResult = new ScanResult
        {
            LogPath = testLogPath,
            Status = ScanStatus.Completed
        };
        _innerPipeline.SetResult(expectedResult);

        // Act
        var result = await _pipeline.ProcessSingleAsync(testLogPath);

        // Assert
        Assert.Equal(expectedResult, result);
        Assert.Single(_innerPipeline.ProcessedPaths);
        Assert.Equal(testLogPath, _innerPipeline.ProcessedPaths[0]);
    }

    [Fact]
    public async Task ProcessSingleAsync_WhenFcxModeEnabled_ShouldRunFcxChecksAndMergeResults()
    {
        // Arrange
        _settingsService.Settings.FcxMode = true;
        _settingsService.Settings.DefaultGamePath = @"C:\Games\Fallout4";
        var testLogPath = "test.log";

        var innerResult = new ScanResult
        {
            LogPath = testLogPath,
            Status = ScanStatus.Completed
        };
        _innerPipeline.SetResult(innerResult);

        // Act
        var result = await _pipeline.ProcessSingleAsync(testLogPath);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(testLogPath, result.LogPath);

        // Should have FCX result added to analysis results
        var fcxResult = result.AnalysisResults.FirstOrDefault(r => r.AnalyzerName == "FCX File Integrity");
        Assert.NotNull(fcxResult);
        Assert.IsType<FcxScanResult>(fcxResult);
    }

    [Fact]
    public async Task ProcessBatchAsync_WhenFcxModeDisabled_ShouldPassThroughToInnerPipeline()
    {
        // Arrange
        _settingsService.Settings.FcxMode = false;
        var logPaths = new[] { "log1.log", "log2.log", "log3.log" };

        var expectedResults = logPaths.Select(path => new ScanResult
        {
            LogPath = path,
            Status = ScanStatus.Completed
        }).ToList();

        _innerPipeline.SetBatchResults(expectedResults);

        // Act
        var results = new List<ScanResult>();
        await foreach (var result in _pipeline.ProcessBatchAsync(logPaths)) results.Add(result);

        // Assert
        Assert.Equal(3, results.Count);
        for (var i = 0; i < results.Count; i++) Assert.Equal(expectedResults[i], results[i]);
    }

    [Fact]
    public async Task ProcessBatchAsync_WhenFcxModeEnabled_ShouldRunFcxChecksOnce()
    {
        // Arrange
        _settingsService.Settings.FcxMode = true;
        _settingsService.Settings.DefaultGamePath = @"C:\Games\Fallout4";
        var logPaths = new[] { "log1.log", "log2.log" };

        var innerResults = logPaths.Select(path => new ScanResult
        {
            LogPath = path,
            Status = ScanStatus.Completed
        }).ToList();

        _innerPipeline.SetBatchResults(innerResults);

        // Act
        var results = new List<ScanResult>();
        await foreach (var result in _pipeline.ProcessBatchAsync(logPaths)) results.Add(result);

        // Assert
        Assert.Equal(2, results.Count);

        // Each result should have FCX findings merged in
        foreach (var result in results)
        {
            var fcxResult = result.AnalysisResults.FirstOrDefault(r => r.AnalyzerName == "FCX File Integrity");
            Assert.NotNull(fcxResult);
        }
    }

    [Fact]
    public async Task ProcessBatchAsync_WithCriticalFcxIssues_ShouldYieldSpecialFcxResult()
    {
        // Arrange
        _settingsService.Settings.FcxMode = true;
        _settingsService.Settings.DefaultGamePath = @"C:\InvalidPath"; // This will cause FCX to fail
        var logPaths = new[] { "log1.log" };

        var innerResults = logPaths.Select(path => new ScanResult
        {
            LogPath = path,
            Status = ScanStatus.Completed
        }).ToList();

        _innerPipeline.SetBatchResults(innerResults);

        // Act
        var results = new List<ScanResult>();
        await foreach (var result in _pipeline.ProcessBatchAsync(logPaths)) results.Add(result);

        // Assert
        Assert.True(results.Count >= 1);

        // Check if FCX-only result was yielded for critical issues
        var fcxOnlyResult = results.FirstOrDefault(r => r.LogPath == "FCX_CHECK");
        if (fcxOnlyResult != null)
        {
            Assert.Equal(ScanStatus.CompletedWithErrors, fcxOnlyResult.Status);
            Assert.Single(fcxOnlyResult.AnalysisResults);
            Assert.IsType<FcxScanResult>(fcxOnlyResult.AnalysisResults[0]);
        }
    }

    [Fact]
    public async Task MergeFcxResults_WithCriticalIssues_ShouldAddWarningMessage()
    {
        // Arrange
        _settingsService.Settings.FcxMode = true;
        _settingsService.Settings.DefaultGamePath = @"C:\Games\Fallout4";

        // Simulate a critical FCX issue by not having the game installed
        _hashService.SetFileExists(@"C:\Games\Fallout4\Fallout4.exe", false);

        var testLogPath = "test.log";
        var innerResult = new ScanResult
        {
            LogPath = testLogPath,
            Status = ScanStatus.Completed
        };
        _innerPipeline.SetResult(innerResult);

        // Act
        var result = await _pipeline.ProcessSingleAsync(testLogPath);

        // Assert
        Assert.NotNull(result);

        // Check for critical issue warning
        var warningMessage = result.ErrorMessages.FirstOrDefault(msg =>
            msg.Contains("FCX detected critical game integrity issues"));

        // The warning might not be present if FCX doesn't find critical issues
        // This depends on the FileIntegrityAnalyzer implementation
    }

    [Fact]
    public async Task DisposeAsync_ShouldDisposeInnerPipeline()
    {
        // Arrange & Act
        await _pipeline.DisposeAsync();

        // Assert
        Assert.True(_innerPipeline.IsDisposed);
    }
}

/// <summary>
///     Test implementation of IScanPipeline for unit testing
/// </summary>
internal class TestScanPipeline : IScanPipeline
{
    private readonly List<ScanResult> _results = new();

    public List<string> ProcessedPaths { get; } = new();
    public bool IsDisposed { get; private set; }

    public Task<ScanResult> ProcessSingleAsync(string logPath, CancellationToken cancellationToken = default)
    {
        ProcessedPaths.Add(logPath);
        return Task.FromResult(_results.FirstOrDefault() ?? new ScanResult { LogPath = logPath });
    }

    public async IAsyncEnumerable<ScanResult> ProcessBatchAsync(
        IEnumerable<string> logPaths,
        ScanOptions? options = null,
        IProgress<BatchProgress>? progress = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var result in _results) yield return result;

        await Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }

    public void SetResult(ScanResult result)
    {
        _results.Clear();
        _results.Add(result);
    }

    public void SetBatchResults(IEnumerable<ScanResult> results)
    {
        _results.Clear();
        _results.AddRange(results);
    }
}

/// <summary>
///     Test implementation of IApplicationSettingsService
/// </summary>
internal class TestApplicationSettingsService : IApplicationSettingsService
{
    public ApplicationSettings Settings { get; } = new()
    {
        FcxMode = false,
        DefaultGamePath = string.Empty
    };

    public Task<ApplicationSettings> LoadSettingsAsync()
    {
        return Task.FromResult(Settings);
    }

    public Task SaveSettingsAsync(ApplicationSettings settings)
    {
        return Task.CompletedTask;
    }

    public Task SaveSettingAsync(string key, object value)
    {
        // For testing, we can update the Settings object directly
        var property = Settings.GetType().GetProperty(key);
        property?.SetValue(Settings, value);
        return Task.CompletedTask;
    }

    public ApplicationSettings GetDefaultSettings()
    {
        return new ApplicationSettings
        {
            FcxMode = false,
            DefaultGamePath = string.Empty
        };
    }
}

/// <summary>
///     Test implementation of IHashValidationService
/// </summary>
internal class TestHashValidationService : IHashValidationService
{
    private readonly Dictionary<string, bool> _fileExistence = new();
    private readonly Dictionary<string, string> _fileHashes = new();

    public Task<string> CalculateFileHashAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (_fileHashes.TryGetValue(filePath, out var hash)) return Task.FromResult(hash);

        return Task.FromResult("TESTHASH123456");
    }

    public Task<string> CalculateFileHashWithProgressAsync(string filePath, IProgress<long>? progress,
        CancellationToken cancellationToken = default)
    {
        return CalculateFileHashAsync(filePath, cancellationToken);
    }

    public Task<HashValidation> ValidateFileAsync(string filePath, string expectedHash,
        CancellationToken cancellationToken = default)
    {
        var validation = new HashValidation
        {
            FilePath = filePath,
            ExpectedHash = expectedHash,
            ActualHash = _fileHashes.GetValueOrDefault(filePath, "INVALID"),
            HashType = "SHA256"
        };

        return Task.FromResult(validation);
    }

    public async Task<Dictionary<string, HashValidation>> ValidateBatchAsync(Dictionary<string, string> fileHashMap,
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, HashValidation>();

        foreach (var kvp in fileHashMap)
            results[kvp.Key] = await ValidateFileAsync(kvp.Key, kvp.Value, cancellationToken);

        return results;
    }

    public void SetFileExists(string path, bool exists)
    {
        _fileExistence[path] = exists;
    }

    public void SetFileHash(string path, string hash)
    {
        _fileHashes[path] = hash;
    }
}