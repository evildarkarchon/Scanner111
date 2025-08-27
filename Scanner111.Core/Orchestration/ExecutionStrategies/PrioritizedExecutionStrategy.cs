using Microsoft.Extensions.Logging;
using Scanner111.Core.Analysis;

namespace Scanner111.Core.Orchestration.ExecutionStrategies;

/// <summary>
///     Executes analyzers in priority groups, with parallel execution within each group.
/// </summary>
public sealed class PrioritizedExecutionStrategy : IExecutionStrategy
{
    private readonly SemaphoreSlim _concurrencyLimiter;
    private readonly ILogger _logger;
    private readonly ParallelExecutionStrategy _parallelStrategy;

    public PrioritizedExecutionStrategy(ILogger logger, SemaphoreSlim? concurrencyLimiter = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _concurrencyLimiter = concurrencyLimiter ?? new SemaphoreSlim(Environment.ProcessorCount);
        _parallelStrategy = new ParallelExecutionStrategy(logger, _concurrencyLimiter);
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
            _logger.LogWarning("No analyzers provided for prioritized execution");
            return Enumerable.Empty<AnalysisResult>();
        }

        // Group analyzers by priority
        var priorityGroups = analyzersList
            .GroupBy(a => a.Priority)
            .OrderBy(g => g.Key)
            .ToList();

        _logger.LogDebug(
            "Starting prioritized execution with {GroupCount} priority groups containing {TotalCount} analyzers",
            priorityGroups.Count,
            analyzersList.Count);

        var allResults = new List<AnalysisResult>();

        // Execute each priority group
        foreach (var group in priorityGroups)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogDebug(
                "Executing priority group {Priority} with {Count} analyzers",
                group.Key,
                group.Count());

            try
            {
                // Execute analyzers within the same priority group in parallel
                var groupResults = await _parallelStrategy.ExecuteAsync(
                    group,
                    context,
                    options,
                    cancellationToken).ConfigureAwait(false);

                allResults.AddRange(groupResults);

                // Check if any analyzer requested to skip further processing
                if (groupResults.Any(r => r.SkipFurtherProcessing))
                {
                    _logger.LogInformation(
                        "Skipping remaining priority groups due to skip request from priority {Priority}",
                        group.Key);
                    break;
                }

                // Check for critical failures that should stop execution
                if (!options.ContinueOnError && groupResults.Any(r => r.Severity == AnalysisSeverity.Critical))
                {
                    _logger.LogWarning(
                        "Stopping execution due to critical failure in priority group {Priority}",
                        group.Key);
                    break;
                }

                // Update context with shared data from completed analyzers
                foreach (var result in groupResults.Where(r => r.Success))
                    if (result.Metadata != null)
                        foreach (var kvp in result.Metadata)
                            context.SetSharedData($"{result.AnalyzerName}.{kvp.Key}", kvp.Value);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Prioritized execution was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing priority group {Priority}", group.Key);

                if (!options.ContinueOnError) throw;

                // Add failure results for all analyzers in the failed group
                foreach (var analyzer in group)
                    allResults.Add(AnalysisResult.CreateFailure(
                        analyzer.Name,
                        $"Priority group {group.Key} execution failed: {ex.Message}"));
            }
        }

        _logger.LogDebug("Prioritized execution completed with {Count} results", allResults.Count);

        return allResults;
    }
}