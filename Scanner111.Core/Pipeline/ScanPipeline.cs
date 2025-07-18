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
    private readonly IYamlSettingsProvider _settingsProvider;
    private readonly SemaphoreSlim _semaphore;
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
                result.AddError("Failed to parse crash log");
                return result;
            }

            result.CrashLog = crashLog;

            // Run analyzers
            var analyzerTasks = new List<Task<AnalysisResult>>();
            
            foreach (var analyzer in _analyzers)
            {
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
            }

            // Wait for all parallel analyzers
            if (analyzerTasks.Any())
            {
                var parallelResults = await Task.WhenAll(analyzerTasks);
                foreach (var analysisResult in parallelResults)
                {
                    result.AddAnalysisResult(analysisResult);
                }
            }

            result.Status = result.HasErrors ? ScanStatus.CompletedWithErrors : ScanStatus.Completed;
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
    }

    public async IAsyncEnumerable<ScanResult> ProcessBatchAsync(
        IEnumerable<string> logPaths,
        ScanOptions? options = null,
        IProgress<BatchProgress>? progress = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        options ??= new ScanOptions();
        var paths = logPaths.ToList();
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
                foreach (var path in paths)
                {
                    await channel.Writer.WriteAsync(path, cancellationToken);
                }
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
            await Task.WhenAll(tasks);
            channel.Writer.Complete();
        }, cancellationToken);

        await foreach (var result in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return result;
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

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        
        _semaphore?.Dispose();
        _disposed = true;
    }
}