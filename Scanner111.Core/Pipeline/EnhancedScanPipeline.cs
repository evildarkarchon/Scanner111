using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Analyzers;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;

namespace Scanner111.Core.Pipeline;

/// <summary>
/// Enhanced scan pipeline with caching, error handling, and detailed progress reporting
/// </summary>
public class EnhancedScanPipeline : IScanPipeline
{
    private readonly IEnumerable<IAnalyzer> _analyzers;
    private readonly ILogger<EnhancedScanPipeline> _logger;
    private readonly IMessageHandler _messageHandler;
    private readonly IYamlSettingsProvider _settingsProvider;
    private readonly ICacheManager _cacheManager;
    private readonly ResilientExecutor _resilientExecutor;
    private readonly SemaphoreSlim _semaphore;
    private bool _disposed;

    public EnhancedScanPipeline(
        IEnumerable<IAnalyzer> analyzers,
        ILogger<EnhancedScanPipeline> logger,
        IMessageHandler messageHandler,
        IYamlSettingsProvider settingsProvider,
        ICacheManager cacheManager,
        ResilientExecutor resilientExecutor)
    {
        _analyzers = analyzers.OrderBy(a => a.Priority).ToList();
        _logger = logger;
        _messageHandler = messageHandler;
        _settingsProvider = settingsProvider;
        _cacheManager = cacheManager;
        _resilientExecutor = resilientExecutor;
        _semaphore = new SemaphoreSlim(Environment.ProcessorCount);
    }

    public async Task<ScanResult> ProcessSingleAsync(string logPath, CancellationToken cancellationToken = default)
    {
        var executionResult = await _resilientExecutor.ExecuteAsync(async ct =>
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new ScanResult { LogPath = logPath };

            try
            {
                _logger.LogInformation("Starting enhanced scan of {LogPath}", logPath);
                
                // Parse crash log with caching
                var crashLog = await ParseCrashLogWithCaching(logPath, ct);
                if (crashLog == null)
                {
                    result.Status = ScanStatus.Failed;
                    result.AddError("Failed to parse crash log");
                    return result;
                }

                result.CrashLog = crashLog;

                // Run analyzers with caching and error handling
                await RunAnalyzersWithCaching(result, crashLog, ct);

                result.Status = result.HasErrors ? ScanStatus.CompletedWithErrors : ScanStatus.Completed;
                
                _logger.LogInformation("Completed scan of {LogPath} in {ElapsedMs}ms with status {Status}", 
                    logPath, stopwatch.ElapsedMilliseconds, result.Status);
            }
            catch (OperationCanceledException)
            {
                result.Status = ScanStatus.Cancelled;
                _logger.LogWarning("Scan cancelled for {LogPath}", logPath);
            }
            catch (Exception ex)
            {
                result.Status = ScanStatus.Failed;
                result.AddError($"Unhandled exception: {ex.Message}");
                _logger.LogError(ex, "Error scanning {LogPath}", logPath);
            }
            finally
            {
                result.ProcessingTime = stopwatch.Elapsed;
            }

            return result;
        }, $"ProcessSingle:{logPath}", cancellationToken);
        
        return executionResult ?? new ScanResult 
        { 
            LogPath = logPath, 
            Status = ScanStatus.Failed 
        };
    }

    public async IAsyncEnumerable<ScanResult> ProcessBatchAsync(
        IEnumerable<string> logPaths,
        ScanOptions? options = null,
        IProgress<BatchProgress>? progress = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        options ??= new ScanOptions();
        var paths = logPaths.ToList();
        var detailedProgress = new DetailedProgress();
        var startTime = DateTime.UtcNow;
        
        // Initialize progress
        var initialProgress = new DetailedProgressInfo
        {
            TotalFiles = paths.Count,
            StartTime = startTime,
            LastUpdateTime = startTime,
            MemoryUsageMB = GC.GetTotalMemory(false) / (1024 * 1024)
        };
        detailedProgress.Report(initialProgress);

        // Create channel for producer/consumer pattern
        var channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = true
        });

        // Producer task
        var producerTask = Task.Run(async () =>
        {
            try
            {
                foreach (var path in paths)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await channel.Writer.WriteAsync(path, cancellationToken);
                }
            }
            finally
            {
                channel.Writer.Complete();
            }
        }, cancellationToken);

        // Consumer tasks with detailed progress tracking
        var maxConcurrency = options.MaxDegreeOfParallelism ?? options.MaxConcurrency;
        var consumerTasks = Enumerable.Range(0, maxConcurrency)
            .Select(i => ProcessChannelWithProgressAsync(channel.Reader, detailedProgress, cancellationToken))
            .ToList();

        // Process results as they complete
        var processedFiles = 0;
        var successfulScans = 0;
        var failedScans = 0;
        var incompleteScans = 0;

        await foreach (var result in MergeResultsAsync(consumerTasks, cancellationToken))
        {
            processedFiles++;
            
            switch (result.Status)
            {
                case ScanStatus.Completed:
                    successfulScans++;
                    break;
                case ScanStatus.Failed:
                    failedScans++;
                    break;
                case ScanStatus.CompletedWithErrors:
                    incompleteScans++;
                    break;
            }

            // Report batch progress for backward compatibility
            if (progress != null)
            {
                var elapsed = DateTime.UtcNow - startTime;
                var filesRemaining = paths.Count - processedFiles;
                var filesPerSecond = processedFiles / elapsed.TotalSeconds;
                var estimatedTimeRemaining = filesRemaining > 0 && filesPerSecond > 0
                    ? TimeSpan.FromSeconds(filesRemaining / filesPerSecond)
                    : (TimeSpan?)null;

                progress.Report(new BatchProgress
                {
                    TotalFiles = paths.Count,
                    ProcessedFiles = processedFiles,
                    SuccessfulScans = successfulScans,
                    FailedScans = failedScans,
                    IncompleteScans = incompleteScans,
                    CurrentFile = result.LogPath,
                    ElapsedTime = elapsed,
                    EstimatedTimeRemaining = estimatedTimeRemaining
                });
            }

            yield return result;
        }

        await producerTask;
        
        // Log final statistics
        var cacheStats = _cacheManager.GetStatistics();
        _logger.LogInformation(
            "Batch processing completed: {TotalFiles} files, {Success} successful, {Failed} failed, {Incomplete} incomplete. " +
            "Cache hit rate: {HitRate:P1}",
            paths.Count, successfulScans, failedScans, incompleteScans, cacheStats.HitRate);
    }

    private async Task<CrashLog?> ParseCrashLogWithCaching(string logPath, CancellationToken cancellationToken)
    {
        return await _resilientExecutor.ExecuteAsync(async ct =>
        {
            // Check if we have a cached result that's still valid
            if (_cacheManager.IsFileCacheValid(logPath))
            {
                // For crash log parsing, we could cache the parsed result if needed
                // For now, just parse normally but with resilient execution
            }

            return await CrashLog.ParseAsync(logPath, ct);
        }, $"ParseCrashLog:{logPath}", cancellationToken);
    }

    private async Task RunAnalyzersWithCaching(ScanResult result, CrashLog crashLog, CancellationToken cancellationToken)
    {
        var analyzerTasks = new List<Task<AnalysisResult?>>();
        
        foreach (var analyzer in _analyzers)
        {
            if (analyzer.CanRunInParallel)
            {
                analyzerTasks.Add(RunAnalyzerWithCaching(analyzer, crashLog, cancellationToken));
            }
            else
            {
                // Run sequential analyzers immediately and wait
                var analysisResult = await RunAnalyzerWithCaching(analyzer, crashLog, cancellationToken);
                if (analysisResult != null)
                {
                    result.AddAnalysisResult(analysisResult);
                }
            }
        }

        // Wait for all parallel analyzers
        if (analyzerTasks.Any())
        {
            var parallelResults = await Task.WhenAll(analyzerTasks);
            foreach (var analysisResult in parallelResults)
            {
                if (analysisResult != null)
                {
                    result.AddAnalysisResult(analysisResult);
                }
            }
        }
    }

    private async Task<AnalysisResult?> RunAnalyzerWithCaching(
        IAnalyzer analyzer,
        CrashLog crashLog,
        CancellationToken cancellationToken)
    {
        // Check cache first
        var cachedResult = _cacheManager.GetCachedAnalysisResult(crashLog.FilePath, analyzer.Name);
        if (cachedResult != null)
        {
            _logger.LogTrace("Using cached result for {Analyzer}:{FilePath}", analyzer.Name, crashLog.FilePath);
            return cachedResult;
        }

        return await _resilientExecutor.ExecuteAsync(async ct =>
        {
            _logger.LogDebug("Running analyzer: {AnalyzerName} on {FilePath}", analyzer.Name, crashLog.FilePath);
            
            var result = await analyzer.AnalyzeAsync(crashLog, ct);
            
            // Cache the result if successful
            if (result.Success)
            {
                _cacheManager.CacheAnalysisResult(crashLog.FilePath, analyzer.Name, result);
            }
            
            return result;
        }, $"RunAnalyzer:{analyzer.Name}:{crashLog.FilePath}", cancellationToken);
    }

    private async IAsyncEnumerable<ScanResult> ProcessChannelWithProgressAsync(
        ChannelReader<string> reader,
        DetailedProgress detailedProgress,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var logPath in reader.ReadAllAsync(cancellationToken))
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                detailedProgress.ReportFileStart(logPath);
                
                var result = await ProcessSingleAsync(logPath, cancellationToken);
                
                detailedProgress.ReportFileComplete(logPath, !result.Failed);
                
                yield return result;
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }

    private async IAsyncEnumerable<ScanResult> MergeResultsAsync(
        List<IAsyncEnumerable<ScanResult>> sources,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<ScanResult>();
        var tasks = sources.Select(async source =>
        {
            await foreach (var item in source.WithCancellation(cancellationToken))
            {
                await channel.Writer.WriteAsync(item, cancellationToken);
            }
        }).ToList();

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.WhenAll(tasks);
            }
            finally
            {
                channel.Writer.Complete();
            }
        }, cancellationToken);

        await foreach (var result in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return result;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        
        _semaphore?.Dispose();
        _disposed = true;
    }
}