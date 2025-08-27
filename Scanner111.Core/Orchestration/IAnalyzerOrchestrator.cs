using Scanner111.Core.Analysis;

namespace Scanner111.Core.Orchestration;

/// <summary>
///     Defines the contract for orchestrating analyzer execution.
/// </summary>
public interface IAnalyzerOrchestrator
{
    /// <summary>
    ///     Runs analysis using all registered analyzers.
    /// </summary>
    /// <param name="request">The analysis request containing input and configuration.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The orchestration result containing all analysis results and the final report.</returns>
    Task<OrchestrationResult> RunAnalysisAsync(
        AnalysisRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Runs analysis using specific analyzers.
    /// </summary>
    /// <param name="request">The analysis request.</param>
    /// <param name="analyzerNames">Names of specific analyzers to run.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The orchestration result.</returns>
    Task<OrchestrationResult> RunAnalysisAsync(
        AnalysisRequest request,
        IEnumerable<string> analyzerNames,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets the list of registered analyzer names.
    /// </summary>
    /// <returns>Collection of analyzer names.</returns>
    Task<IEnumerable<string>> GetRegisteredAnalyzersAsync();

    /// <summary>
    ///     Validates whether the orchestrator can process the given request.
    /// </summary>
    /// <param name="request">The request to validate.</param>
    /// <returns>Validation result.</returns>
    Task<ValidationResult> ValidateRequestAsync(AnalysisRequest request);
}

/// <summary>
///     Represents a request for analysis orchestration.
/// </summary>
public sealed class AnalysisRequest
{
    /// <summary>
    ///     Gets or sets the input path (file or directory) to analyze.
    /// </summary>
    public required string InputPath { get; init; }

    /// <summary>
    ///     Gets or sets the type of analysis to perform.
    /// </summary>
    public AnalysisType AnalysisType { get; init; } = AnalysisType.CrashLog;

    /// <summary>
    ///     Gets or sets the execution options.
    /// </summary>
    public OrchestrationOptions? Options { get; init; }

    /// <summary>
    ///     Gets or sets additional metadata for the request.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }
}

/// <summary>
///     Represents the result of request validation.
/// </summary>
public sealed class ValidationResult
{
    /// <summary>
    ///     Gets whether the request is valid.
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    ///     Gets validation error messages if any.
    /// </summary>
    public List<string>? Errors { get; init; }

    /// <summary>
    ///     Gets validation warnings if any.
    /// </summary>
    public List<string>? Warnings { get; init; }

    /// <summary>
    ///     Creates a successful validation result.
    /// </summary>
    public static ValidationResult Success()
    {
        return new ValidationResult { IsValid = true };
    }

    /// <summary>
    ///     Creates a failed validation result.
    /// </summary>
    public static ValidationResult Failure(params string[] errors)
    {
        return new ValidationResult
        {
            IsValid = false,
            Errors = new List<string>(errors)
        };
    }
}