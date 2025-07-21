using Microsoft.Extensions.Logging;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;

namespace Scanner111.Core.Pipeline;

/// <summary>
///     Example showing how to use the enhanced scan pipeline
/// </summary>
public static class PipelineUsageExample
{
    /// <summary>
    ///     Example of creating and using an enhanced pipeline with all features enabled
    /// </summary>
    public static async Task<IEnumerable<ScanResult>> ProcessFilesWithEnhancedPipelineAsync(
        IEnumerable<string> logFiles,
        IProgress<BatchProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // Create the enhanced pipeline with all features enabled
        var pipeline = new ScanPipelineBuilder()
            .AddDefaultAnalyzers()
            .WithCaching()
            .WithEnhancedErrorHandling()
            .WithPerformanceMonitoring()
            .WithLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            })
            .Build();

        var results = new List<ScanResult>();

        // Process files with progress reporting
        await foreach (var result in pipeline.ProcessBatchAsync(logFiles,
                           new ScanOptions
                           {
                               MaxConcurrency = Environment.ProcessorCount,
                               EnableCaching = true
                           },
                           progress,
                           cancellationToken))
            results.Add(result);

        await pipeline.DisposeAsync();
        return results;
    }

    /// <summary>
    ///     Example of processing a single file with detailed progress reporting
    /// </summary>
    public static async Task<ScanResult> ProcessSingleFileWithProgressAsync(
        string logFile,
        IProgress<DetailedProgressInfo>? detailedProgress = null,
        CancellationToken cancellationToken = default)
    {
        using var enhancedCts = new EnhancedCancellationTokenSource(
            TimeSpan.FromMinutes(5));

        var combinedToken = CancellationTokenSource
            .CreateLinkedTokenSource(cancellationToken, enhancedCts.Token)
            .Token;

        var pipeline = new ScanPipelineBuilder()
            .AddDefaultAnalyzers()
            .WithCaching()
            .WithEnhancedErrorHandling()
            .WithPerformanceMonitoring()
            .Build();

        try
        {
            // Use checkpoint for cancellation support
            await combinedToken.CheckpointAsync("Starting file processing",
                new Progress<string>(_ => detailedProgress?.Report(new DetailedProgressInfo
                {
                    CurrentFile = logFile,
                    CurrentFileStatus = FileProcessingStatus.InProgress,
                    LastUpdateTime = DateTime.UtcNow
                })));

            var result = await pipeline.ProcessSingleAsync(logFile, combinedToken);

            detailedProgress?.Report(new DetailedProgressInfo
            {
                CurrentFile = logFile,
                CurrentFileStatus = result.Failed ? FileProcessingStatus.Failed : FileProcessingStatus.Completed,
                ProcessedFiles = 1,
                TotalFiles = 1,
                LastUpdateTime = DateTime.UtcNow
            });

            return result;
        }
        finally
        {
            await pipeline.DisposeAsync();
        }
    }

    /// <summary>
    ///     Example of using cache management
    /// </summary>
    public static async Task DemonstrateAdvancedCachingAsync()
    {
        var pipeline = new ScanPipelineBuilder()
            .AddDefaultAnalyzers()
            .WithCaching()
            .WithEnhancedErrorHandling()
            .Build();

        if (pipeline is EnhancedScanPipeline)
            // Enhanced pipeline created with caching enabled
            MessageHandler.MsgDebug("Enhanced pipeline created with caching enabled");

        await pipeline.DisposeAsync();
    }

    /// <summary>
    ///     Example of error handling configuration
    /// </summary>
    public static IScanPipeline CreatePipelineWithCustomErrorHandling()
    {
        return new ScanPipelineBuilder()
            .AddDefaultAnalyzers()
            .WithEnhancedErrorHandling()
            .WithLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Debug);
            })
            .Build();
    }

    /// <summary>
    ///     Example of lightweight pipeline for simple scenarios
    /// </summary>
    public static IScanPipeline CreateLightweightPipeline()
    {
        return new ScanPipelineBuilder()
            .AddDefaultAnalyzers()
            .WithCaching(false)
            .WithEnhancedErrorHandling(false)
            .WithPerformanceMonitoring(false)
            .Build();
    }
}