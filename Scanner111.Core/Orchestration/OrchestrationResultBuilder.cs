using Scanner111.Core.Analysis;

namespace Scanner111.Core.Orchestration;

/// <summary>
///     Builder for creating immutable OrchestrationResult instances.
/// </summary>
public sealed class OrchestrationResultBuilder
{
    private readonly Dictionary<string, TimeSpan> _analyzerTimings;
    private readonly Dictionary<string, object> _metrics;
    private readonly List<AnalysisResult> _results;
    private readonly DateTime _startTime;
    private readonly Guid _correlationId;
    
    private bool _success;
    private string? _finalReport;
    private TimeSpan _totalDuration;
    private DateTime? _endTime;

    public OrchestrationResultBuilder()
    {
        _results = new List<AnalysisResult>();
        _analyzerTimings = new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase);
        _metrics = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        _startTime = DateTime.UtcNow;
        _correlationId = Guid.NewGuid();
    }

    public OrchestrationResultBuilder(OrchestrationResult existing)
    {
        if (existing == null)
            throw new ArgumentNullException(nameof(existing));

        _results = new List<AnalysisResult>(existing.Results);
        _analyzerTimings = new Dictionary<string, TimeSpan>(existing.AnalyzerTimings, StringComparer.OrdinalIgnoreCase);
        _metrics = new Dictionary<string, object>(existing.Metrics, StringComparer.OrdinalIgnoreCase);
        _startTime = existing.StartTime;
        _correlationId = existing.CorrelationId;
        _success = existing.Success;
        _finalReport = existing.FinalReport;
        _totalDuration = existing.TotalDuration;
        _endTime = existing.EndTime;
    }

    public OrchestrationResultBuilder WithSuccess(bool success)
    {
        _success = success;
        return this;
    }

    public OrchestrationResultBuilder WithFinalReport(string? finalReport)
    {
        _finalReport = finalReport;
        return this;
    }

    public OrchestrationResultBuilder WithTotalDuration(TimeSpan totalDuration)
    {
        _totalDuration = totalDuration;
        return this;
    }

    public OrchestrationResultBuilder WithEndTime(DateTime? endTime)
    {
        _endTime = endTime;
        return this;
    }

    public OrchestrationResultBuilder AddResult(AnalysisResult result)
    {
        if (result == null)
            throw new ArgumentNullException(nameof(result));

        _results.Add(result);

        // Track timing if available
        if (result.Duration.HasValue)
            _analyzerTimings[result.AnalyzerName] = result.Duration.Value;

        return this;
    }

    public OrchestrationResultBuilder AddResults(IEnumerable<AnalysisResult> results)
    {
        if (results == null)
            throw new ArgumentNullException(nameof(results));

        foreach (var result in results)
            AddResult(result);

        return this;
    }

    public OrchestrationResultBuilder AddMetric(string key, object value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Metric key cannot be null or whitespace.", nameof(key));

        _metrics[key] = value;
        return this;
    }

    public OrchestrationResultBuilder AddMetrics(IEnumerable<KeyValuePair<string, object>> metrics)
    {
        if (metrics == null)
            throw new ArgumentNullException(nameof(metrics));

        foreach (var kvp in metrics)
            AddMetric(kvp.Key, kvp.Value);

        return this;
    }

    public OrchestrationResult Build()
    {
        return new OrchestrationResult(
            _correlationId,
            _success,
            _results.ToList(),
            _finalReport,
            _totalDuration,
            _startTime,
            _endTime,
            new Dictionary<string, TimeSpan>(_analyzerTimings),
            new Dictionary<string, object>(_metrics));
    }
}