using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Analyzers;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;

namespace Scanner111.Core.Pipeline;

/// <summary>
/// Represents an advanced scanning pipeline that integrates multiple analyzers,
/// supports caching mechanisms, handles errors resiliently, and provides
/// features for detailed batch and single log processing with progress reporting.
/// </summary>
public class EnhancedScanPipeline : IScanPipeline
{
    private readonly IEnumerable<IAnalyzer> _analyzers;
    private readonly ICacheManager _cacheManager;
    private readonly ILogger<EnhancedScanPipeline> _logger;
    private readonly IMessageHandler _messageHandler;
    private readonly ResilientExecutor _resilientExecutor;
    private readonly SemaphoreSlim _semaphore;
    private readonly IYamlSettingsProvider _settingsProvider;
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

    /// Asynchronously processes a single log file at the specified path and returns the result of the scan.
    /// <param name="logPath">The file path of the log to be processed.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <return>A task representing the asynchronous operation and returning a <see cref="ScanResult"/> object containing the result of the scan.</return>
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
                    var crashLog = await ParseCrashLogWithCaching(logPath, ct).ConfigureAwait(false);
                    if (crashLog == null)
                    {
                        result.Status = ScanStatus.Failed;
                        result.AddError("Failed to parse crash log");
                        return result;
                    }

                    result.CrashLog = crashLog;

                    // Run analyzers with caching and error handling
                    await RunAnalyzersWithCaching(result, crashLog, ct).ConfigureAwait(false);

                    result.Status = result.HasErrors ? ScanStatus.CompletedWithErrors : ScanStatus.Completed;

                    // Free memory immediately after analysis is complete
                    result.CrashLog?.DisposeOriginalLines();

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

    /// Asynchronously processes a batch of log files specified in the provided paths, yielding scan results as they become available.
    /// <param name="logPaths">A collection of file paths representing the logs to be processed.</param>
    /// <param name="options">Optional settings for processing the batch of logs, such as concurrency and other scan configurations.</param>
    /// <param name="progress">An optional progress reporter used to track the progress of the batch processing.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <return>An asynchronous stream of <see cref="ScanResult"/> objects, each representing the result of processing a log file.</return>
    public async IAsyncEnumerable<ScanResult> ProcessBatchAsync(
        IEnumerable<string> logPaths,
        ScanOptions? options = null,
        IProgress<BatchProgress>? progress = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        options ??= new ScanOptions();
        // Deduplicate input paths to prevent processing the same file multiple times
        var paths = logPaths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var detailedProgress = new DetailedProgress();
        var startTime = DateTime.UtcNow;

        // Initialize progress
        var initialProgress = new DetailedProgressInfo
        {
            TotalFiles = paths.Count,
            StartTime = startTime,
            LastUpdateTime = startTime,
            MemoryUsageMb = GC.GetTotalMemory(false) / (1024 * 1024)
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
                    await channel.Writer.WriteAsync(path, cancellationToken).ConfigureAwait(false);
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
            .Select(_ => ProcessChannelWithProgressAsync(channel.Reader, detailedProgress, cancellationToken))
            .ToList();

        // Process results as they complete
        var processedFiles = 0;
        var successfulScans = 0;
        var failedScans = 0;
        var incompleteScans = 0;

        await foreach (var result in MergeResultsAsync(consumerTasks, cancellationToken).ConfigureAwait(false))
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
                case ScanStatus.Pending:
                case ScanStatus.InProgress:
                case ScanStatus.Cancelled:
                    break;
                default:
                    throw new ArgumentOutOfRangeException("Invalid scan status: " + result.Status);
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

        await producerTask.ConfigureAwait(false);

        // Log final statistics
        var cacheStats = _cacheManager.GetStatistics();
        _logger.LogInformation(
            "Batch processing completed: {TotalFiles} files, {Success} successful, {Failed} failed, {Incomplete} incomplete. " +
            "Cache hit rate: {HitRate:P1}",
            paths.Count, successfulScans, failedScans, incompleteScans, cacheStats.HitRate);
    }

    /// Asynchronously releases the resources used by the EnhancedScanPipeline instance.
    /// Ensures all resources such as the semaphore are properly disposed and suppresses finalization of the object.
    /// Subsequent calls to dispose methods on the same instance will have no effect as disposal is idempotent.
    /// <return>A value task that represents the asynchronous disposal operation.</return>
    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;

        _semaphore.Dispose();
        _disposed = true;

        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    /// Parses a crash log from the specified log file path using caching mechanisms, and returns the parsed result.
    /// <param name="logPath">The file path of the crash log to be parsed.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <return>A task representing the asynchronous operation, returning a <see cref="CrashLog"/> object if parsing is successful; otherwise, null.</return>
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

                return await CrashLog.ParseAsync(logPath, ct).ConfigureAwait(false);
            }, $"ParseCrashLog:{logPath}", cancellationToken);
    }

    /// Executes a set of analyzers on the specified crash log, utilizing caching for efficiency, and updates the provided scan result with the analysis outcomes.
    /// <param name="result">The result object that will be updated with analysis results.</param>
    /// <param name="crashLog">The crash log to be analyzed by the configured analyzers.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <return>A task that represents the asynchronous operation of running the analyzers and updating the scan result.</return>
    private async Task RunAnalyzersWithCaching(ScanResult result, CrashLog crashLog,
        CancellationToken cancellationToken)
    {
        var analyzerTasks = new List<Task<AnalysisResult?>>();

        foreach (var analyzer in _analyzers)
            if (analyzer.CanRunInParallel)
            {
                analyzerTasks.Add(RunAnalyzerWithCaching(analyzer, crashLog, cancellationToken));
            }
            else
            {
                // Run sequential analyzers immediately and wait
                var analysisResult = await RunAnalyzerWithCaching(analyzer, crashLog, cancellationToken).ConfigureAwait(false);
                if (analysisResult != null) result.AddAnalysisResult(analysisResult);
            }

        // Wait for all parallel analyzers
        if (analyzerTasks.Any())
        {
            var parallelResults = await Task.WhenAll(analyzerTasks).ConfigureAwait(false);
            foreach (var analysisResult in parallelResults)
                if (analysisResult != null)
                    result.AddAnalysisResult(analysisResult);
        }
    }

    /// Executes an analyzer on a crash log using caching to improve performance and returns the resulting analysis.
    /// <param name="analyzer">The analyzer instance to be executed on the crash log.</param>
    /// <param name="crashLog">The crash log input to be analyzed.</param>
    /// <param name="cancellationToken">A token to monitor for operation cancellation.</param>
    /// <return>A task representing the asynchronous operation, returning an <see cref="AnalysisResult"/> object if the analysis succeeds, or null if a cached result is unavailable and no analysis is performed.</return>
    private async Task<AnalysisResult?> RunAnalyzerWithCaching(
        IAnalyzer analyzer,
        CrashLog crashLog,
        CancellationToken cancellationToken)
    {
        // Check cache first
        var cachedResult = _cacheManager.GetCachedAnalysisResult(crashLog.FilePath, analyzer.Name);
        if (cachedResult == null)
            return await _resilientExecutor.ExecuteAsync(async ct =>
                {
                    _logger.LogDebug("Running analyzer: {AnalyzerName} on {FilePath}", analyzer.Name,
                        crashLog.FilePath);

                    var result = await analyzer.AnalyzeAsync(crashLog, ct);

                    // Cache the result if successful
                    if (result.Success) _cacheManager.CacheAnalysisResult(crashLog.FilePath, analyzer.Name, result);

                    return result;
                }, $"RunAnalyzer:{analyzer.Name}:{crashLog.FilePath}", cancellationToken);
        _logger.LogTrace("Using cached result for {Analyzer}:{FilePath}", analyzer.Name, crashLog.FilePath);
        return cachedResult;

    }

    /// Processes log files read from the specified channel and reports detailed progress during processing.
    /// <param name="reader">The channel reader used to retrieve log file paths.</param>
    /// <param name="detailedProgress">An object for reporting detailed progress of the operation.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <return>An asynchronous enumerable of <see cref="ScanResult"/> representing the results of processing each log file.</return>
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

    /// Merges multiple asynchronous sequences of scan results into a single asynchronous sequence in a streaming manner.
    /// <param name="sources">A list of asynchronous sequences of <see cref="ScanResult"/> objects to be merged.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <return>An asynchronous enumerable sequence containing the merged stream of <see cref="ScanResult"/> objects from all sources.</return>
    private async IAsyncEnumerable<ScanResult> MergeResultsAsync(
        List<IAsyncEnumerable<ScanResult>> sources,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Create streaming merge without unbounded buffering
        var enumerators = sources.Select(source => source.GetAsyncEnumerator(cancellationToken)).ToList();
        var activeTasks = new List<Task<(int sourceIndex, bool hasValue, ScanResult? result)>>();

        try
        {
            // Start initial MoveNext for all sources
            activeTasks.AddRange(enumerators.Select((_, enumeratorIndex) => enumeratorIndex)
                .Select(index => Task.Run(async () =>
                {
                    try
                    {
                        var hasValue = await enumerators[index].MoveNextAsync();
                        return (index, hasValue, hasValue ? enumerators[index].Current : null);
                    }
                    catch (OperationCanceledException)
                    {
                        return (index, false, null);
                    }
                    catch
                    {
                        return (index, false, null);
                    }
                }, cancellationToken)));

            // Process results as they become available
            while (activeTasks.Count > 0)
            {
                var completedTask = await Task.WhenAny(activeTasks);
                activeTasks.Remove(completedTask);

                var (sourceIndex, hasValue, result) = await completedTask;

                if (hasValue && result != null)
                {
                    // Yield the result immediately - no buffering
                    yield return result;

                    // Start next read from this source
                    activeTasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            var nextHasValue = await enumerators[sourceIndex].MoveNextAsync();
                            return (sourceIndex, nextHasValue, nextHasValue ? enumerators[sourceIndex].Current : null);
                        }
                        catch (OperationCanceledException)
                        {
                            return (sourceIndex, false, null);
                        }
                        catch
                        {
                            return (sourceIndex, false, null);
                        }
                    }, cancellationToken));
                }
                // If hasValue is false, this source is exhausted - don't restart it
            }
        }
        finally
        {
            // Dispose all enumerators
            foreach (var enumerator in enumerators)
                try
                {
                    await enumerator.DisposeAsync();
                }
                catch (NotSupportedException)
                {
                    // Some enumerators may not support async disposal
                    // This is acceptable during cancellation scenarios
                }
                catch (ObjectDisposedException)
                {
                    // Enumerator may already be disposed
                }
        }
    }
}