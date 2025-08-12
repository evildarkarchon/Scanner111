using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Analyzers;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;

namespace Scanner111.Core.Pipeline;

/// <summary>
/// Represents a pipeline that processes scan operations, allowing single log processing or batch log processing
/// with analyzers and resource management capabilities.
/// </summary>
public class ScanPipeline : IScanPipeline
{
    private readonly IEnumerable<IAnalyzer> _analyzers;
    private readonly ILogger<ScanPipeline> _logger;
    private readonly IMessageHandler _messageHandler;
    private readonly SemaphoreSlim _semaphore;
    private readonly IYamlSettingsProvider _settingsProvider;
    private bool _disposed;

    public ScanPipeline(
        IEnumerable<IAnalyzer> analyzers,
        ILogger<ScanPipeline> logger,
        IMessageHandler messageHandler,
        IYamlSettingsProvider settingsProvider)
    {
        _analyzers = analyzers; // Store IEnumerable without materializing
        _logger = logger;
        _messageHandler = messageHandler;
        _settingsProvider = settingsProvider;
        _semaphore = new SemaphoreSlim(Environment.ProcessorCount);
    }

    /// <summary>
    /// Processes a single crash log file asynchronously, analyzes its contents using configured analyzers,
    /// and returns the result, including processing status and analysis outcomes.
    /// </summary>
    /// <param name="logPath">The path of the crash log file to be processed.</param>
    /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="ScanResult"/> object containing the results of the analysis, processing status,
    /// and other relevant details.</returns>
    public async Task<ScanResult> ProcessSingleAsync(string logPath, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ScanResult { LogPath = logPath };

        try
        {
            _logger.LogInformation("Starting scan of {LogPath}", logPath);

            // Parse crash log
            var crashLog = await CrashLog.ParseAsync(logPath, cancellationToken).ConfigureAwait(false);
            if (crashLog == null)
            {
                result.Status = ScanStatus.Failed;
                var errorMessage = $"Failed to parse crash log: {Path.GetFileName(logPath)}";
                result.AddError(errorMessage);
                _messageHandler.ShowError(errorMessage, MessageTarget.All);
                return result;
            }

            result.CrashLog = crashLog;

            // Run analyzers
            var analyzerTasks = new List<Task<AnalysisResult>>();

            foreach (var analyzer in _analyzers.OrderBy(a => a.Priority))
                if (analyzer.CanRunInParallel)
                {
                    analyzerTasks.Add(RunAnalyzerAsync(analyzer, crashLog, cancellationToken));
                }
                else
                {
                    // Run sequential analyzers immediately and wait
                    var analysisResult = await RunAnalyzerAsync(analyzer, crashLog, cancellationToken).ConfigureAwait(false);
                    result.AddAnalysisResult(analysisResult);
                }

            // Wait for all parallel analyzers
            if (analyzerTasks.Any())
            {
                var parallelResults = await Task.WhenAll(analyzerTasks).ConfigureAwait(false);
                foreach (var analysisResult in parallelResults) result.AddAnalysisResult(analysisResult);
            }

            // Check if any analyzers failed
            var hasAnalyzerErrors = result.AnalysisResults.Any(ar => !ar.Success);
            result.Status = result.HasErrors || hasAnalyzerErrors ? ScanStatus.CompletedWithErrors : ScanStatus.Completed;

            // Free memory immediately after analysis is complete
            result.CrashLog?.DisposeOriginalLines();
        }
        catch (OperationCanceledException)
        {
            result.Status = ScanStatus.Cancelled;
            _logger.LogWarning("Scan cancelled for {LogPath}", logPath);
            _messageHandler.ShowWarning($"Scan cancelled for: {Path.GetFileName(logPath)}", MessageTarget.All);
        }
        catch (Exception ex)
        {
            result.Status = ScanStatus.Failed;
            var errorMessage = $"Unhandled exception while scanning {Path.GetFileName(logPath)}: {ex.Message}";
            result.AddError(errorMessage);
            _logger.LogError(ex, "Error scanning {LogPath}", logPath);
            _messageHandler.ShowError(errorMessage, MessageTarget.All);
        }
        finally
        {
            result.ProcessingTime = stopwatch.Elapsed;
        }

        return result;
    }

    /// <summary>
    /// Processes a batch of crash log files asynchronously, analyzes their contents using configured analyzers,
    /// and returns a stream of analysis results.
    /// </summary>
    /// <param name="logPaths">A collection of file paths representing the crash logs to process.</param>
    /// <param name="options">Optional configuration settings for the processing pipeline, such as concurrency limits.</param>
    /// <param name="progress">Optional progress reporter to track the progress of the batch processing operation.</param>
    /// <param name="cancellationToken">Optional cancellation token to cancel the batch processing operation.</param>
    /// <returns>An asynchronous stream of <see cref="ScanResult"/> objects, each representing the outcome of analyzing
    /// an individual crash log file.</returns>
    public async IAsyncEnumerable<ScanResult> ProcessBatchAsync(
        IEnumerable<string> logPaths,
        ScanOptions? options = null,
        IProgress<BatchProgress>? progress = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        options ??= new ScanOptions();
        // Deduplicate input paths to prevent processing the same file multiple times
        var paths = logPaths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var totalFiles = paths.Count;
        var processedFiles = 0;
        var successfulScans = 0;
        var failedScans = 0;
        var incompleteScans = 0;
        var startTime = DateTime.UtcNow;

        // Create bounded channel with backpressure for producer/consumer pattern
        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = true
        });

        // Producer task
        var producerTask = Task.Run(async () =>
        {
            try
            {
                foreach (var path in paths) await channel.Writer.WriteAsync(path, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                channel.Writer.Complete();
            }
        }, cancellationToken);

        // Consumer tasks
        var maxConcurrency = options.MaxDegreeOfParallelism ?? options.MaxConcurrency;
        var consumerTasks = Enumerable.Range(0, maxConcurrency)
            .Select(_ => ProcessChannelAsync(channel.Reader, cancellationToken))
            .ToList();

        // Process results as they complete
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
            }

            // Report progress
            if (progress != null)
            {
                var elapsed = DateTime.UtcNow - startTime;
                var filesRemaining = totalFiles - processedFiles;
                var filesPerSecond = processedFiles / elapsed.TotalSeconds;
                var estimatedTimeRemaining = filesRemaining > 0 && filesPerSecond > 0
                    ? TimeSpan.FromSeconds(filesRemaining / filesPerSecond)
                    : (TimeSpan?)null;

                progress.Report(new BatchProgress
                {
                    TotalFiles = totalFiles,
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
    }

    /// <summary>
    /// Disposes resources used by the <see cref="ScanPipeline"/> asynchronously,
    /// releasing any unmanaged and managed resources. Ensures that subsequent
    /// dispose calls are safe and do not lead to repeated disposal.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous disposal operation.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        _semaphore?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
        await Task.CompletedTask;
    }

    /// <summary>
    /// Processes log files read from a channel asynchronously, analyzing each file using configured analyzers
    /// and yielding the analysis results as they are completed.
    /// </summary>
    /// <param name="reader">The channel reader instance to read log file paths from.</param>
    /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
    /// <returns>An asynchronous stream of <see cref="ScanResult"/> objects representing the results of the analyzed log files.</returns>
    private async IAsyncEnumerable<ScanResult> ProcessChannelAsync(
        ChannelReader<string> reader,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var logPath in reader.ReadAllAsync(cancellationToken))
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                yield return await ProcessSingleAsync(logPath, cancellationToken);
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }

    /// <summary>
    /// Merges multiple asynchronous streams of <see cref="ScanResult"/> into a single unified asynchronous sequence.
    /// The method ensures that results are streamed without unbounded buffering, maintaining order as they become available.
    /// </summary>
    /// <param name="sources">A list of asynchronous enumerables that represent the individual result streams to merge.</param>
    /// <param name="cancellationToken">A cancellation token to signal cancellation of the operation.</param>
    /// <returns>An asynchronous enumerable that sequentially yields merged <see cref="ScanResult"/> objects from all sources.</returns>
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
            for (var i = 0; i < enumerators.Count; i++)
            {
                var index = i;
                activeTasks.Add(Task.Run(async () =>
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
                }, cancellationToken));
            }

            // Process results as they become available
            while (activeTasks.Count > 0)
            {
                var completedTask = await Task.WhenAny(activeTasks);
                activeTasks.Remove(completedTask);

                var (sourceIndex, hasValue, result) = await completedTask;

                if (!hasValue || result == null) continue;
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

    /// <summary>
    /// Executes the specified analyzer on the provided crash log asynchronously and returns the analysis result.
    /// </summary>
    /// <param name="analyzer">The analyzer to be executed on the crash log.</param>
    /// <param name="crashLog">The crash log to be analyzed.</param>
    /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
    /// <returns>An <see cref="AnalysisResult"/> object containing the analyzer's output, including success status and errors, if any.</returns>
    private async Task<AnalysisResult> RunAnalyzerAsync(
        IAnalyzer analyzer,
        CrashLog crashLog,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Running analyzer: {AnalyzerName}", analyzer.Name);
            return await analyzer.AnalyzeAsync(crashLog, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in analyzer {AnalyzerName}", analyzer.Name);
            return new GenericAnalysisResult
            {
                AnalyzerName = analyzer.Name,
                Success = false,
                Errors = [$"Analyzer failed: {ex.Message}"]
            };
        }
    }
}