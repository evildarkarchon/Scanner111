namespace Scanner111.Core.Analysis;

/// <summary>
///     Defines the contract for all analyzers in the Scanner111 system.
/// </summary>
public interface IAnalyzer
{
    /// <summary>
    ///     Gets the unique name of this analyzer.
    /// </summary>
    string Name { get; }

    /// <summary>
    ///     Gets the display name for this analyzer used in reports.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    ///     Gets the execution priority. Lower values execute first.
    /// </summary>
    int Priority { get; }

    /// <summary>
    ///     Gets whether this analyzer is enabled.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    ///     Gets the timeout for this analyzer's execution.
    /// </summary>
    TimeSpan Timeout { get; }

    /// <summary>
    ///     Performs the analysis operation asynchronously.
    /// </summary>
    /// <param name="context">The context containing input data and shared resources.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The result of the analysis.</returns>
    Task<AnalysisResult> AnalyzeAsync(AnalysisContext context, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Validates whether this analyzer can run with the given context.
    /// </summary>
    /// <param name="context">The context to validate.</param>
    /// <returns>True if the analyzer can run, false otherwise.</returns>
    Task<bool> CanAnalyzeAsync(AnalysisContext context);
}