using Scanner111.Core.Analysis;

namespace Scanner111.Core.Orchestration.ExecutionStrategies;

/// <summary>
///     Defines the contract for analyzer execution strategies.
/// </summary>
public interface IExecutionStrategy
{
    /// <summary>
    ///     Executes the given analyzers according to the strategy implementation.
    /// </summary>
    /// <param name="analyzers">The analyzers to execute.</param>
    /// <param name="context">The analysis context.</param>
    /// <param name="options">Orchestration options.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Collection of analysis results.</returns>
    Task<IEnumerable<AnalysisResult>> ExecuteAsync(
        IEnumerable<IAnalyzer> analyzers,
        AnalysisContext context,
        OrchestrationOptions options,
        CancellationToken cancellationToken);
}