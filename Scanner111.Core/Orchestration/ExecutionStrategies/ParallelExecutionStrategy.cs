using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Analysis;

namespace Scanner111.Core.Orchestration.ExecutionStrategies;

/// <summary>
/// Executes analyzers in parallel with configurable concurrency limits.
/// </summary>
public sealed class ParallelExecutionStrategy : IExecutionStrategy
{
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _concurrencyLimiter;
    
    public ParallelExecutionStrategy(ILogger logger, SemaphoreSlim? concurrencyLimiter = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _concurrencyLimiter = concurrencyLimiter ?? new SemaphoreSlim(Environment.ProcessorCount);
    }
    
    /// <inheritdoc />
    public async Task<IEnumerable<AnalysisResult>> ExecuteAsync(
        IEnumerable<IAnalyzer> analyzers,
        AnalysisContext context,
        OrchestrationOptions options,
        CancellationToken cancellationToken)
    {
        var analyzersList = analyzers?.ToList() ?? throw new ArgumentNullException(nameof(analyzers));
        
        if (!analyzersList.Any())
        {
            _logger.LogWarning("No analyzers provided for parallel execution");
            return Enumerable.Empty<AnalysisResult>();
        }
        
        _logger.LogDebug("Starting parallel execution of {Count} analyzers", analyzersList.Count);
        
        var results = new ConcurrentBag<AnalysisResult>();
        var maxDegreeOfParallelism = options.MaxDegreeOfParallelism > 0 
            ? options.MaxDegreeOfParallelism 
            : Environment.ProcessorCount;
        
        // Configure parallel options
        var parallelOptions = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = maxDegreeOfParallelism
        };
        
        // Execute analyzers in parallel
        await Parallel.ForEachAsync(
            analyzersList,
            parallelOptions,
            async (analyzer, ct) =>
            {
                await _concurrencyLimiter.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    var result = await ExecuteAnalyzerWithRetryAsync(
                        analyzer,
                        context,
                        options,
                        ct).ConfigureAwait(false);
                    
                    results.Add(result);
                }
                finally
                {
                    _concurrencyLimiter.Release();
                }
            }).ConfigureAwait(false);
        
        _logger.LogDebug("Parallel execution completed with {Count} results", results.Count);
        
        return results;
    }
    
    private async Task<AnalysisResult> ExecuteAnalyzerWithRetryAsync(
        IAnalyzer analyzer,
        AnalysisContext context,
        OrchestrationOptions options,
        CancellationToken cancellationToken)
    {
        var attempt = 0;
        var maxAttempts = options.EnableRetry ? options.MaxRetryAttempts : 1;
        
        while (attempt < maxAttempts)
        {
            attempt++;
            
            try
            {
                _logger.LogDebug("Executing analyzer {Name} (attempt {Attempt}/{Max})", 
                    analyzer.Name, attempt, maxAttempts);
                
                var result = await analyzer.AnalyzeAsync(context, cancellationToken)
                    .ConfigureAwait(false);
                
                if (result.Success || !options.EnableRetry)
                {
                    return result;
                }
                
                // If failed and retry is enabled, wait before retrying
                if (attempt < maxAttempts)
                {
                    var delay = TimeSpan.FromMilliseconds(
                        options.RetryDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));
                    
                    _logger.LogWarning(
                        "Analyzer {Name} failed on attempt {Attempt}. Retrying in {Delay}ms",
                        analyzer.Name, attempt, delay.TotalMilliseconds);
                    
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Analyzer {Name} was cancelled", analyzer.Name);
                return AnalysisResult.CreateSkipped(analyzer.Name, "Execution was cancelled");
            }
            catch (Exception ex)
            {
                if (attempt >= maxAttempts || !options.EnableRetry)
                {
                    _logger.LogError(ex, "Analyzer {Name} failed after {Attempts} attempts", 
                        analyzer.Name, attempt);
                    
                    if (!options.ContinueOnError)
                        throw;
                    
                    return AnalysisResult.CreateFailure(analyzer.Name, ex.Message);
                }
                
                _logger.LogWarning(ex, 
                    "Analyzer {Name} encountered error on attempt {Attempt}. Will retry.",
                    analyzer.Name, attempt);
                
                await Task.Delay(options.RetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }
        
        // Should not reach here, but return failure as safety
        return AnalysisResult.CreateFailure(
            analyzer.Name, 
            $"Failed after {maxAttempts} attempts");
    }
}