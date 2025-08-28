using Scanner111.Core.Analysis;

namespace Scanner111.Core.Orchestration;

/// <summary>
///     Represents the complete result of an orchestrated analysis run.
///     This class is immutable - use OrchestrationResultBuilder to create instances.
/// </summary>
public sealed class OrchestrationResult
{
    private readonly Dictionary<string, TimeSpan> _analyzerTimings;
    private readonly Dictionary<string, object> _metrics;
    private readonly List<AnalysisResult> _results;

    /// <summary>
    /// Creates a new immutable OrchestrationResult.
    /// Use OrchestrationResultBuilder for easier construction.
    /// </summary>
    internal OrchestrationResult(
        Guid correlationId,
        bool success,
        List<AnalysisResult> results,
        string? finalReport,
        TimeSpan totalDuration,
        DateTime startTime,
        DateTime? endTime,
        Dictionary<string, TimeSpan> analyzerTimings,
        Dictionary<string, object> metrics)
    {
        CorrelationId = correlationId;
        Success = success;
        _results = results ?? new List<AnalysisResult>();
        FinalReport = finalReport;
        TotalDuration = totalDuration;
        StartTime = startTime;
        EndTime = endTime;
        _analyzerTimings = analyzerTimings ?? new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase);
        _metrics = metrics ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }
    
    /// <summary>
    /// Creates an empty OrchestrationResult.
    /// Prefer using OrchestrationResultBuilder for actual results.
    /// </summary>
    public OrchestrationResult()
    {
        _results = new List<AnalysisResult>();
        _analyzerTimings = new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase);
        _metrics = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        StartTime = DateTime.UtcNow;
        CorrelationId = Guid.NewGuid();
    }

    /// <summary>
    ///     Gets the unique correlation ID for this orchestration run.
    /// </summary>
    public Guid CorrelationId { get; }

    /// <summary>
    ///     Gets whether the orchestration completed successfully.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    ///     Gets the individual analyzer results.
    /// </summary>
    public IReadOnlyList<AnalysisResult> Results => _results.AsReadOnly();

    /// <summary>
    ///     Gets the final composed report.
    /// </summary>
    public string? FinalReport { get; init; }

    /// <summary>
    ///     Gets the total duration of the orchestration.
    /// </summary>
    public TimeSpan TotalDuration { get; init; }

    /// <summary>
    ///     Gets the start time of the orchestration.
    /// </summary>
    public DateTime StartTime { get; }

    /// <summary>
    ///     Gets the end time of the orchestration.
    /// </summary>
    public DateTime? EndTime { get; init; }

    /// <summary>
    ///     Gets timing information for each analyzer.
    /// </summary>
    public IReadOnlyDictionary<string, TimeSpan> AnalyzerTimings => _analyzerTimings;

    /// <summary>
    ///     Gets collected metrics from the orchestration run.
    /// </summary>
    public IReadOnlyDictionary<string, object> Metrics => _metrics;

    /// <summary>
    ///     Gets the number of successful analyzers.
    /// </summary>
    public int SuccessfulAnalyzers => _results.Count(r => r.Success);

    /// <summary>
    ///     Gets the number of failed analyzers.
    /// </summary>
    public int FailedAnalyzers => _results.Count(r => !r.Success && !r.SkipFurtherProcessing);

    /// <summary>
    ///     Gets the number of skipped analyzers.
    /// </summary>
    public int SkippedAnalyzers => _results.Count(r => r.SkipFurtherProcessing);

    /// <summary>
    ///     Gets the highest severity level from all results.
    /// </summary>
    public AnalysisSeverity HighestSeverity => _results.Any()
        ? _results.Max(r => r.Severity)
        : AnalysisSeverity.None;

    /// <summary>
    ///     Gets all error messages from all analyzers.
    /// </summary>
    public IEnumerable<string> AllErrors => _results.SelectMany(r => r.Errors);

    /// <summary>
    ///     Gets all warning messages from all analyzers.
    /// </summary>
    public IEnumerable<string> AllWarnings => _results.SelectMany(r => r.Warnings);

    /// <summary>
    ///     Adds an analysis result to the orchestration result.
    ///     Note: This modifies the collection. For immutable operations, use OrchestrationResultBuilder.
    /// </summary>
    public void AddResult(AnalysisResult result)
    {
        if (result == null)
            throw new ArgumentNullException(nameof(result));

        _results.Add(result);

        // Track timing if available
        if (result.Duration.HasValue) _analyzerTimings[result.AnalyzerName] = result.Duration.Value;
    }

    /// <summary>
    ///     Adds multiple analysis results.
    ///     Note: This modifies the collection. For immutable operations, use OrchestrationResultBuilder.
    /// </summary>
    public void AddResults(IEnumerable<AnalysisResult> results)
    {
        if (results == null)
            throw new ArgumentNullException(nameof(results));

        foreach (var result in results) AddResult(result);
    }

    /// <summary>
    ///     Adds a metric to the result.
    ///     Note: This modifies the collection. For immutable operations, use OrchestrationResultBuilder.
    /// </summary>
    public void AddMetric(string key, object value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Metric key cannot be null or whitespace.", nameof(key));

        _metrics[key] = value;
    }

    /// <summary>
    ///     Gets a result for a specific analyzer by name.
    /// </summary>
    public AnalysisResult? GetAnalyzerResult(string analyzerName)
    {
        return _results.FirstOrDefault(r =>
            string.Equals(r.AnalyzerName, analyzerName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Creates a summary of the orchestration result.
    /// </summary>
    public string CreateSummary()
    {
        var lines = new List<string>
        {
            "Orchestration Result Summary",
            "============================",
            $"Correlation ID: {CorrelationId}",
            $"Status: {(Success ? "Success" : "Failed")}",
            $"Duration: {TotalDuration:mm\\:ss\\.fff}",
            $"Highest Severity: {HighestSeverity}",
            "",
            "Analyzer Results:",
            $"  Successful: {SuccessfulAnalyzers}",
            $"  Failed: {FailedAnalyzers}",
            $"  Skipped: {SkippedAnalyzers}",
            $"  Total: {_results.Count}"
        };

        if (AllErrors.Any())
        {
            lines.Add("");
            lines.Add("Errors:");
            foreach (var error in AllErrors) lines.Add($"  - {error}");
        }

        if (AllWarnings.Any())
        {
            lines.Add("");
            lines.Add("Warnings:");
            foreach (var warning in AllWarnings.Take(5)) lines.Add($"  - {warning}");

            var remainingWarnings = AllWarnings.Count() - 5;
            if (remainingWarnings > 0) lines.Add($"  ... and {remainingWarnings} more warnings");
        }

        return string.Join(Environment.NewLine, lines);
    }
}