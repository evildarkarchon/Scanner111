using Microsoft.Extensions.Logging;
using Scanner111.Core.Analysis;

namespace Scanner111.Core.Orchestration.ExecutionStrategies;

/// <summary>
///     Executes analyzers sequentially in order.
/// </summary>
public sealed class SequentialExecutionStrategy : IExecutionStrategy
{
    private readonly ILogger _logger;

    public SequentialExecutionStrategy(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
            _logger.LogWarning("No analyzers provided for sequential execution");
            return Enumerable.Empty<AnalysisResult>();
        }

        _logger.LogDebug("Starting sequential execution of {Count} analyzers", analyzersList.Count);

        var results = new List<AnalysisResult>();

        foreach (var analyzer in analyzersList)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                _logger.LogDebug("Executing analyzer {Name}", analyzer.Name);

                var result = await ExecuteAnalyzerWithRetryAsync(
                    analyzer,
                    context,
                    options,
                    cancellationToken).ConfigureAwait(false);

                results.Add(result);

                // Check if we should stop further processing
                if (result.SkipFurtherProcessing)
                {
                    _logger.LogInformation(
                        "Analyzer {Name} requested to skip further processing",
                        analyzer.Name);
                    break;
                }

                // Stop on error if configured
                if (!result.Success && !options.ContinueOnError)
                {
                    _logger.LogWarning(
                        "Stopping sequential execution due to analyzer {Name} failure",
                        analyzer.Name);
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Sequential execution was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Analyzer {Name} encountered an error", analyzer.Name);

                if (!options.ContinueOnError) throw;

                results.Add(AnalysisResult.CreateFailure(analyzer.Name, ex.Message));
            }
        }

        _logger.LogDebug("Sequential execution completed with {Count} results", results.Count);

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
                var result = await analyzer.AnalyzeAsync(context, cancellationToken)
                    .ConfigureAwait(false);

                if (result.Success || !options.EnableRetry) return result;

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
                throw;
            }
            catch (Exception ex)
            {
                if (attempt >= maxAttempts || !options.EnableRetry)
                {
                    _logger.LogError(ex, "Analyzer {Name} failed after {Attempts} attempts",
                        analyzer.Name, attempt);
                    throw;
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