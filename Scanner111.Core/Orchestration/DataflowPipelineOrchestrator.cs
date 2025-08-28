using System.Diagnostics;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Analysis;
using Scanner111.Core.Configuration;
using Scanner111.Core.Reporting;

namespace Scanner111.Core.Orchestration;

/// <summary>
///     High-performance pipeline orchestrator using TPL Dataflow.
///     Eliminates Python's GIL workarounds with true parallel execution.
/// </summary>
public sealed class DataflowPipelineOrchestrator : IAsyncDisposable
{
    private readonly ILogger<DataflowPipelineOrchestrator> _logger;
    private readonly IReportComposer _reportComposer;
    private readonly IAsyncYamlSettingsCore _yamlCore;
    private readonly CancellationTokenSource _pipelineCts;
    private readonly Dictionary<string, TimeSpan> _stageMetrics;
    private readonly SemaphoreSlim _metricsLock;
    private bool _disposed;

    public DataflowPipelineOrchestrator(
        ILogger<DataflowPipelineOrchestrator> logger,
        IReportComposer reportComposer,
        IAsyncYamlSettingsCore yamlCore)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _reportComposer = reportComposer ?? throw new ArgumentNullException(nameof(reportComposer));
        _yamlCore = yamlCore ?? throw new ArgumentNullException(nameof(yamlCore));
        _pipelineCts = new CancellationTokenSource();
        _stageMetrics = new Dictionary<string, TimeSpan>();
        _metricsLock = new SemaphoreSlim(1, 1);
    }

    /// <summary>
    ///     Creates an optimized processing pipeline using TPL Dataflow.
    ///     No GIL means we can use true parallelism without workarounds.
    /// </summary>
    public async Task<BatchProcessingResult> ProcessBatchAsync(
        IEnumerable<AnalysisRequest> requests,
        IReadOnlyList<IAnalyzer> analyzers,
        PipelineOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        options ??= PipelineOptions.Default;
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _pipelineCts.Token);
        var overallStopwatch = Stopwatch.StartNew();
        var results = new List<OrchestrationResult>();

        try
        {
            _logger.LogInformation("Starting TPL Dataflow pipeline with {AnalyzerCount} analyzers", analyzers.Count);

            // Stage 1: Batch requests for optimal processing
            var batchBlock = new BatchBlock<AnalysisRequest>(
                options.BatchSize,
                new GroupingDataflowBlockOptions
                {
                    BoundedCapacity = options.BoundedCapacity,
                    CancellationToken = linkedCts.Token
                });

            // Stage 2: Load and prepare data in parallel
            var loadBlock = new TransformBlock<AnalysisRequest[], LoadedBatch>(
                async batch => await LoadBatchAsync(batch, linkedCts.Token),
                new ExecutionDataflowBlockOptions
                {
                    BoundedCapacity = options.BoundedCapacity,
                    MaxDegreeOfParallelism = options.MaxLoadParallelism,
                    CancellationToken = linkedCts.Token
                });

            // Stage 3: Process through analyzers in parallel
            // No need for Python's CPU count detection - TPL handles it optimally
            var processBlock = new TransformManyBlock<LoadedBatch, PipelineAnalysisResult>(
                async batch => await ProcessWithAnalyzersAsync(batch, analyzers, linkedCts.Token),
                new ExecutionDataflowBlockOptions
                {
                    BoundedCapacity = options.BoundedCapacity,
                    MaxDegreeOfParallelism = options.MaxAnalysisParallelism,
                    CancellationToken = linkedCts.Token
                });

            // Stage 4: Generate reports
            var reportBlock = new TransformBlock<PipelineAnalysisResult, OrchestrationResult>(
                async result => await GenerateReportAsync(result, linkedCts.Token),
                new ExecutionDataflowBlockOptions
                {
                    BoundedCapacity = options.BoundedCapacity,
                    MaxDegreeOfParallelism = options.MaxReportParallelism,
                    CancellationToken = linkedCts.Token
                });

            // Stage 5: Collect results
            var collectorBlock = new ActionBlock<OrchestrationResult>(
                result => results.Add(result),
                new ExecutionDataflowBlockOptions
                {
                    BoundedCapacity = options.BoundedCapacity,
                    MaxDegreeOfParallelism = 1, // Sequential collection
                    CancellationToken = linkedCts.Token
                });

            // Link the pipeline stages with propagation
            var linkOptions = new DataflowLinkOptions { PropagateCompletion = true };
            batchBlock.LinkTo(loadBlock, linkOptions);
            loadBlock.LinkTo(processBlock, linkOptions);
            processBlock.LinkTo(reportBlock, linkOptions);
            reportBlock.LinkTo(collectorBlock, linkOptions);

            // Post requests to the pipeline
            foreach (var request in requests)
            {
                if (!await batchBlock.SendAsync(request, linkedCts.Token))
                {
                    _logger.LogWarning("Failed to post request to pipeline: {Path}", request.InputPath);
                }
            }

            // Signal completion and wait
            batchBlock.Complete();
            await collectorBlock.Completion;

            overallStopwatch.Stop();

            // Calculate performance metrics
            var metrics = await GetPipelineMetricsAsync();
            
            return new BatchProcessingResult
            {
                Success = true,
                Results = results,
                TotalTime = overallStopwatch.Elapsed,
                ThroughputPerSecond = results.Count / overallStopwatch.Elapsed.TotalSeconds,
                StageMetrics = metrics,
                ProcessedCount = results.Count
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Pipeline processing cancelled");
            return new BatchProcessingResult
            {
                Success = false,
                Results = results,
                TotalTime = overallStopwatch.Elapsed,
                ProcessedCount = results.Count
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pipeline processing failed");
            return new BatchProcessingResult
            {
                Success = false,
                Results = results,
                TotalTime = overallStopwatch.Elapsed,
                ProcessedCount = results.Count,
                Error = ex.Message
            };
        }
        finally
        {
            linkedCts.Dispose();
        }
    }

    private async Task<LoadedBatch> LoadBatchAsync(
        AnalysisRequest[] requests,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var contexts = new List<AnalysisContext>();

        // True parallel loading - no GIL limitations
        await Parallel.ForEachAsync(
            requests,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Environment.ProcessorCount
            },
            async (request, ct) =>
            {
                // Create analysis context with YamlCore
                var context = new AnalysisContext(request.InputPath, _yamlCore);
                
                // Load file data if needed
                if (File.Exists(request.InputPath))
                {
                    var content = await File.ReadAllTextAsync(request.InputPath, ct);
                    context.SetSharedData("FileContent", content);
                }
                
                lock (contexts)
                {
                    contexts.Add(context);
                }
            });

        stopwatch.Stop();
        await RecordMetricAsync("LoadStage", stopwatch.Elapsed);

        return new LoadedBatch
        {
            Requests = requests,
            Contexts = contexts
        };
    }

    private async Task<IEnumerable<PipelineAnalysisResult>> ProcessWithAnalyzersAsync(
        LoadedBatch batch,
        IReadOnlyList<IAnalyzer> analyzers,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var results = new List<PipelineAnalysisResult>();

        // Process each context through all analyzers
        // No need for Python's sleep delays or manual batch sizing
        foreach (var context in batch.Contexts)
        {
            var fragments = new List<ReportFragment>();
            
            // Run analyzers in parallel groups based on priority
            var priorityGroups = analyzers.GroupBy(a => a.Priority).OrderBy(g => g.Key);
            
            foreach (var group in priorityGroups)
            {
                var groupTasks = group.Select(async analyzer =>
                {
                    try
                    {
                        return await analyzer.AnalyzeAsync(context, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Analyzer {Name} failed", analyzer.Name);
                        return null;
                    }
                });

                var groupResults = await Task.WhenAll(groupTasks);
                foreach (var result in groupResults.Where(r => r != null))
                {
                    if (result!.Fragment != null)
                    {
                        fragments.Add(result.Fragment);
                    }
                }
            }

            results.Add(new PipelineAnalysisResult
            {
                Context = context,
                Fragments = fragments
            });
        }

        stopwatch.Stop();
        await RecordMetricAsync("ProcessStage", stopwatch.Elapsed);

        return results;
    }

    private async Task<OrchestrationResult> GenerateReportAsync(
        PipelineAnalysisResult result,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        var report = await _reportComposer.ComposeFromFragmentsAsync(
            result.Fragments,
            new ReportOptions { IncludeTimingInfo = true });

        stopwatch.Stop();
        await RecordMetricAsync("ReportStage", stopwatch.Elapsed);

        var orchestrationResult = new OrchestrationResult
        {
            Success = true,
            FinalReport = report,
            TotalDuration = stopwatch.Elapsed
        };
        
        // Add analyzer results if we have context metadata
        if (result.Context != null)
        {
            foreach (var fragment in result.Fragments)
            {
                var analysisResult = new AnalysisResult(fragment.Title ?? "Unknown")
                {
                    Success = true,
                    Fragment = fragment,
                    Duration = stopwatch.Elapsed
                };
                orchestrationResult.AddResult(analysisResult);
            }
        }

        return orchestrationResult;
    }

    private async Task RecordMetricAsync(string stageName, TimeSpan duration)
    {
        await _metricsLock.WaitAsync();
        try
        {
            _stageMetrics[stageName] = duration;
        }
        finally
        {
            _metricsLock.Release();
        }
    }

    private async Task<Dictionary<string, TimeSpan>> GetPipelineMetricsAsync()
    {
        await _metricsLock.WaitAsync();
        try
        {
            return new Dictionary<string, TimeSpan>(_stageMetrics);
        }
        finally
        {
            _metricsLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _pipelineCts?.Cancel();
        _pipelineCts?.Dispose();
        _metricsLock?.Dispose();
        
        _disposed = true;
        await Task.CompletedTask;
    }

    // Helper classes
    private sealed class LoadedBatch
    {
        public required AnalysisRequest[] Requests { get; init; }
        public required List<AnalysisContext> Contexts { get; init; }
    }

    private sealed class PipelineAnalysisResult
    {
        public required AnalysisContext Context { get; init; }
        public required List<ReportFragment> Fragments { get; init; }
    }
}

/// <summary>
///     Pipeline configuration options optimized for C# threading model.
/// </summary>
public sealed class PipelineOptions
{
    /// <summary>
    ///     Batch size for request grouping.
    ///     Unlike Python, we don't need dynamic sizing based on CPU count.
    /// </summary>
    public int BatchSize { get; set; } = 10;

    /// <summary>
    ///     Bounded capacity for backpressure control.
    ///     TPL Dataflow handles this automatically.
    /// </summary>
    public int BoundedCapacity { get; set; } = 100;

    /// <summary>
    ///     Max parallelism for load stage.
    ///     -1 means use all available cores.
    /// </summary>
    public int MaxLoadParallelism { get; set; } = -1;

    /// <summary>
    ///     Max parallelism for analysis stage.
    /// </summary>
    public int MaxAnalysisParallelism { get; set; } = -1;

    /// <summary>
    ///     Max parallelism for report generation.
    /// </summary>
    public int MaxReportParallelism { get; set; } = Environment.ProcessorCount / 2;

    /// <summary>
    ///     Default options for production use.
    /// </summary>
    public static PipelineOptions Default => new()
    {
        BatchSize = 10,
        BoundedCapacity = 100,
        MaxLoadParallelism = -1,
        MaxAnalysisParallelism = -1,
        MaxReportParallelism = Environment.ProcessorCount / 2
    };

    /// <summary>
    ///     High-throughput options for large batch processing.
    /// </summary>
    public static PipelineOptions HighThroughput => new()
    {
        BatchSize = 50,
        BoundedCapacity = 500,
        MaxLoadParallelism = -1,
        MaxAnalysisParallelism = -1,
        MaxReportParallelism = -1
    };
}

/// <summary>
///     Result of batch processing through the pipeline.
/// </summary>
public sealed class BatchProcessingResult
{
    public bool Success { get; init; }
    public required List<OrchestrationResult> Results { get; init; }
    public TimeSpan TotalTime { get; init; }
    public double ThroughputPerSecond { get; init; }
    public Dictionary<string, TimeSpan>? StageMetrics { get; init; }
    public int ProcessedCount { get; init; }
    public string? Error { get; init; }
}