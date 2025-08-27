using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Analysis;
using Scanner111.Core.Configuration;
using Scanner111.Core.Orchestration.ExecutionStrategies;
using Scanner111.Core.Reporting;

namespace Scanner111.Core.Orchestration;

/// <summary>
/// Orchestrates the execution of multiple analyzers in a coordinated manner.
/// Thread-safe and supports parallel execution.
/// </summary>
public sealed class AnalyzerOrchestrator : IAnalyzerOrchestrator, IAsyncDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AnalyzerOrchestrator> _logger;
    private readonly IAsyncYamlSettingsCore _yamlCore;
    private readonly IReportComposer _reportComposer;
    private readonly SemaphoreSlim _concurrencyLimiter;
    private readonly List<IAnalyzer> _analyzers;
    private bool _disposed;
    
    public AnalyzerOrchestrator(
        IServiceProvider serviceProvider,
        ILogger<AnalyzerOrchestrator> logger,
        IAsyncYamlSettingsCore yamlCore,
        IReportComposer reportComposer)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _yamlCore = yamlCore ?? throw new ArgumentNullException(nameof(yamlCore));
        _reportComposer = reportComposer ?? throw new ArgumentNullException(nameof(reportComposer));
        
        _analyzers = new List<IAnalyzer>();
        _concurrencyLimiter = new SemaphoreSlim(Environment.ProcessorCount);
        
        // Discover and register all analyzers
        DiscoverAnalyzers();
    }
    
    /// <inheritdoc />
    public async Task<OrchestrationResult> RunAnalysisAsync(
        AnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        // Validate request
        var validation = await ValidateRequestAsync(request).ConfigureAwait(false);
        if (!validation.IsValid)
        {
            return new OrchestrationResult
            {
                Success = false,
                FinalReport = $"Validation failed: {string.Join(", ", validation.Errors ?? new List<string>())}"
            };
        }
        
        var enabledAnalyzers = _analyzers.Where(a => a.IsEnabled).ToList();
        return await RunAnalysisInternalAsync(request, enabledAnalyzers, cancellationToken)
            .ConfigureAwait(false);
    }
    
    /// <inheritdoc />
    public async Task<OrchestrationResult> RunAnalysisAsync(
        AnalysisRequest request,
        IEnumerable<string> analyzerNames,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        var selectedAnalyzers = _analyzers
            .Where(a => analyzerNames.Contains(a.Name, StringComparer.OrdinalIgnoreCase))
            .ToList();
        
        if (!selectedAnalyzers.Any())
        {
            return new OrchestrationResult
            {
                Success = false,
                FinalReport = "No matching analyzers found"
            };
        }
        
        return await RunAnalysisInternalAsync(request, selectedAnalyzers, cancellationToken)
            .ConfigureAwait(false);
    }
    
    /// <inheritdoc />
    public Task<IEnumerable<string>> GetRegisteredAnalyzersAsync()
    {
        return Task.FromResult(_analyzers.Select(a => a.Name));
    }
    
    /// <inheritdoc />
    public async Task<ValidationResult> ValidateRequestAsync(AnalysisRequest request)
    {
        var errors = new List<string>();
        
        if (request == null)
        {
            errors.Add("Request cannot be null");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.InputPath))
            {
                errors.Add("Input path cannot be empty");
            }
            else if (!File.Exists(request.InputPath) && !Directory.Exists(request.InputPath))
            {
                errors.Add($"Input path does not exist: {request.InputPath}");
            }
        }
        
        await Task.CompletedTask.ConfigureAwait(false);
        
        return errors.Any() 
            ? ValidationResult.Failure(errors.ToArray())
            : ValidationResult.Success();
    }
    
    private async Task<OrchestrationResult> RunAnalysisInternalAsync(
        AnalysisRequest request,
        List<IAnalyzer> analyzersToRun,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new OrchestrationResult();
        var options = request.Options ?? OrchestrationOptions.Production;
        
        try
        {
            _logger.LogInformation(
                "Starting orchestration for {InputPath} with {AnalyzerCount} analyzers using {Strategy} strategy",
                request.InputPath,
                analyzersToRun.Count,
                options.Strategy);
            
            // Create analysis context
            var context = new AnalysisContext(request.InputPath, _yamlCore, request.AnalysisType);
            
            // Add request metadata to context if provided
            if (request.Metadata != null)
            {
                foreach (var kvp in request.Metadata)
                {
                    context.SetMetadata(kvp.Key, kvp.Value);
                }
            }
            
            // Select and execute strategy
            var strategy = CreateExecutionStrategy(options.Strategy);
            var analysisResults = await strategy.ExecuteAsync(
                analyzersToRun,
                context,
                options,
                cancellationToken).ConfigureAwait(false);
            
            // Collect results
            result.AddResults(analysisResults);
            
            // Compose final report
            if (options.VerboseOutput || result.SuccessfulAnalyzers > 0)
            {
                var report = await _reportComposer.ComposeReportAsync(
                    analysisResults,
                    new ReportOptions { IncludeTimingInfo = options.IncludeTimingInfo })
                    .ConfigureAwait(false);
                
                // Save current state
                var currentResults = result.Results.ToList();
                var currentMetrics = result.Metrics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                
                result = new OrchestrationResult()
                {
                    Success = result.Success,
                    FinalReport = report,
                    TotalDuration = result.TotalDuration,
                    EndTime = result.EndTime
                };
                // Copy all results and metrics
                result.AddResults(currentResults);
                foreach (var kvp in currentMetrics)
                    result.AddMetric(kvp.Key, kvp.Value);
            }
            
            // Mark as successful if we have any successful analyzers
            var successResult = new OrchestrationResult()
            {
                Success = result.SuccessfulAnalyzers > 0 || !result.Results.Any(),
                FinalReport = result.FinalReport,
                TotalDuration = result.TotalDuration,
                EndTime = DateTime.UtcNow
            };
            // Copy all results and metrics from the current result
            foreach (var res in result.Results)
                successResult.AddResult(res);
            foreach (var kvp in result.Metrics)
                successResult.AddMetric(kvp.Key, kvp.Value);
            result = successResult;
            
            // Add metrics
            if (options.CollectMetrics)
            {
                result.AddMetric("TotalAnalyzers", analyzersToRun.Count);
                result.AddMetric("Strategy", options.Strategy.ToString());
                result.AddMetric("MaxParallelism", options.MaxDegreeOfParallelism);
                result.AddMetric("InputSize", GetFileSize(request.InputPath));
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Orchestration was cancelled");
            var cancelledResult = new OrchestrationResult()
            {
                Success = false,
                FinalReport = "Analysis was cancelled",
                TotalDuration = result.TotalDuration,
                EndTime = DateTime.UtcNow
            };
            // Copy all results and metrics
            foreach (var res in result.Results)
                cancelledResult.AddResult(res);
            foreach (var kvp in result.Metrics)
                cancelledResult.AddMetric(kvp.Key, kvp.Value);
            result = cancelledResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Orchestration failed with error");
            var failedResult = new OrchestrationResult()
            {
                Success = false,
                FinalReport = $"Orchestration failed: {ex.Message}",
                TotalDuration = result.TotalDuration,
                EndTime = DateTime.UtcNow
            };
            // Copy all results and metrics
            foreach (var res in result.Results)
                failedResult.AddResult(res);
            foreach (var kvp in result.Metrics)
                failedResult.AddMetric(kvp.Key, kvp.Value);
            result = failedResult;
        }
        finally
        {
            stopwatch.Stop();
            var finalResult = new OrchestrationResult()
            {
                Success = result.Success,
                FinalReport = result.FinalReport,
                TotalDuration = stopwatch.Elapsed,
                EndTime = DateTime.UtcNow
            };
            // Copy all results and metrics
            foreach (var res in result.Results)
                finalResult.AddResult(res);
            foreach (var kvp in result.Metrics)
                finalResult.AddMetric(kvp.Key, kvp.Value);
            result = finalResult;
            
            _logger.LogInformation(
                "Orchestration completed in {Duration}ms with {SuccessCount}/{TotalCount} successful analyzers",
                stopwatch.ElapsedMilliseconds,
                result.SuccessfulAnalyzers,
                result.Results.Count);
        }
        
        return result;
    }
    
    private IExecutionStrategy CreateExecutionStrategy(ExecutionStrategy strategy)
    {
        return strategy switch
        {
            ExecutionStrategy.Sequential => new SequentialExecutionStrategy(_logger),
            ExecutionStrategy.Parallel => new ParallelExecutionStrategy(_logger, _concurrencyLimiter),
            ExecutionStrategy.Prioritized => new PrioritizedExecutionStrategy(_logger, _concurrencyLimiter),
            _ => new ParallelExecutionStrategy(_logger, _concurrencyLimiter)
        };
    }
    
    private void DiscoverAnalyzers()
    {
        // Get all registered IAnalyzer implementations from DI
        var analyzers = _serviceProvider.GetServices<IAnalyzer>().ToList();
        
        if (analyzers.Any())
        {
            _analyzers.AddRange(analyzers);
            _logger.LogInformation("Discovered {Count} analyzers: {Names}",
                analyzers.Count,
                string.Join(", ", analyzers.Select(a => a.Name)));
        }
        else
        {
            _logger.LogWarning("No analyzers were discovered. Register analyzers in DI container.");
        }
    }
    
    private static long GetFileSize(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                return new FileInfo(path).Length;
            }
            
            if (Directory.Exists(path))
            {
                return Directory.GetFiles(path, "*", SearchOption.AllDirectories)
                    .Sum(f => new FileInfo(f).Length);
            }
        }
        catch
        {
            // Ignore errors in size calculation
        }
        
        return 0;
    }
    
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        
        _concurrencyLimiter?.Dispose();
        
        // Dispose any disposable analyzers
        foreach (var analyzer in _analyzers.OfType<IAsyncDisposable>())
        {
            await analyzer.DisposeAsync().ConfigureAwait(false);
        }
        
        foreach (var analyzer in _analyzers.OfType<IDisposable>())
        {
            analyzer.Dispose();
        }
        
        _disposed = true;
    }
}