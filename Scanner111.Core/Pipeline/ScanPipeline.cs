using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Analyzers;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;

namespace Scanner111.Core.Pipeline;

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
        _analyzers = analyzers.OrderBy(a => a.Priority).ToList();
        _logger = logger;
        _messageHandler = messageHandler;
        _settingsProvider = settingsProvider;
        _semaphore = new SemaphoreSlim(Environment.ProcessorCount);
    }

    public async Task<ScanResult> ProcessSingleAsync(string logPath, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ScanResult { LogPath = logPath };

        try
        {
            _logger.LogInformation("Starting scan of {LogPath}", logPath);

            // Parse crash log
            var crashLog = await CrashLog.ParseAsync(logPath, cancellationToken);
            if (crashLog == null)
            {
                result.Status = ScanStatus.Failed;
                var errorMessage = $"Failed to parse crash log: {Path.GetFileName(logPath)}";
                result.AddError(errorMessage);
                _messageHandler.MsgError(errorMessage, MessageTarget.All);
                return result;
            }

            result.CrashLog = crashLog;

            // Run analyzers
            var analyzerTasks = new List<Task<AnalysisResult>>();

            foreach (var analyzer in _analyzers)
                if (analyzer.CanRunInParallel)
                {
                    analyzerTasks.Add(RunAnalyzerAsync(analyzer, crashLog, cancellationToken));
                }
                else
                {
                    // Run sequential analyzers immediately and wait
                    var analysisResult = await RunAnalyzerAsync(analyzer, crashLog, cancellationToken);
                    result.AddAnalysisResult(analysisResult);
                }

            // Wait for all parallel analyzers
            if (analyzerTasks.Any())
            {
                var parallelResults = await Task.WhenAll(analyzerTasks);
                foreach (var analysisResult in parallelResults) result.AddAnalysisResult(analysisResult);
            }

            result.Status = result.HasErrors ? ScanStatus.CompletedWithErrors : ScanStatus.Completed;

            // Free memory immediately after analysis is complete
            result.CrashLog?.DisposeOriginalLines();
        }
        catch (OperationCanceledException)
        {
            result.Status = ScanStatus.Cancelled;
            _logger.LogWarning("Scan cancelled for {LogPath}", logPath);
            _messageHandler.MsgWarning($"Scan cancelled for: {Path.GetFileName(logPath)}", MessageTarget.All);
        }
        catch (Exception ex)
        {
            result.Status = ScanStatus.Failed;
            var errorMessage = $"Unhandled exception while scanning {Path.GetFileName(logPath)}: {ex.Message}";
            result.AddError(errorMessage);
            _logger.LogError(ex, "Error scanning {LogPath}", logPath);
            _messageHandler.MsgError(errorMessage, MessageTarget.All);
        }
        finally
        {
            result.ProcessingTime = stopwatch.Elapsed;
        }

        return result;
    }

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
                foreach (var path in paths) await channel.Writer.WriteAsync(path, cancellationToken);
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

        await producerTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        _semaphore?.Dispose();
        _disposed = true;
        await Task.CompletedTask;
    }

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
                Errors = new[] { $"Analyzer failed: {ex.Message}" }
            };
        }
    }
}