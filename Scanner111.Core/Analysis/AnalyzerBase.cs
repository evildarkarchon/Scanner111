using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Reporting;

namespace Scanner111.Core.Analysis;

/// <summary>
///     Base class for all analyzers providing common functionality.
/// </summary>
public abstract class AnalyzerBase : IAnalyzer
{
    private readonly ILogger _logger;

    protected AnalyzerBase(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public abstract string DisplayName { get; }

    /// <inheritdoc />
    public virtual int Priority => 100;

    /// <inheritdoc />
    public virtual bool IsEnabled => true;

    /// <inheritdoc />
    public virtual TimeSpan Timeout => TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    public async Task<AnalysisResult> AnalyzeAsync(
        AnalysisContext context,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            _logger.LogDebug("Analyzer {Name} is disabled", Name);
            return AnalysisResult.CreateSkipped(Name, "Analyzer is disabled");
        }

        // Validate context
        if (!await CanAnalyzeAsync(context).ConfigureAwait(false))
        {
            _logger.LogDebug("Analyzer {Name} cannot analyze the given context", Name);
            return AnalysisResult.CreateSkipped(Name, "Context validation failed");
        }

        var stopwatch = Stopwatch.StartNew();
        CancellationTokenSource? timeoutCts = null;
        CancellationTokenSource? linkedCts = null;

        try
        {
            // Create a timeout cancellation token
            timeoutCts = new CancellationTokenSource(Timeout);
            linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutCts.Token);

            _logger.LogDebug("Starting analyzer {Name} for {InputPath}", Name, context.InputPath);

            // Perform the actual analysis
            var result = await PerformAnalysisAsync(context, linkedCts.Token)
                .ConfigureAwait(false);

            stopwatch.Stop();

            // Add duration to result
            if (result != null)
            {
                var resultWithDuration = new AnalysisResult(result.AnalyzerName)
                {
                    Success = result.Success,
                    Fragment = result.Fragment,
                    Severity = result.Severity,
                    Duration = stopwatch.Elapsed,
                    SkipFurtherProcessing = result.SkipFurtherProcessing
                };

                // Copy errors and warnings
                foreach (var error in result.Errors)
                    resultWithDuration.AddError(error);
                foreach (var warning in result.Warnings)
                    resultWithDuration.AddWarning(warning);
                foreach (var kvp in result.Metadata)
                    resultWithDuration.AddMetadata(kvp.Key, kvp.Value);

                result = resultWithDuration;

                _logger.LogDebug(
                    "Analyzer {Name} completed in {Duration}ms with status: {Success}",
                    Name,
                    stopwatch.ElapsedMilliseconds,
                    result.Success);
            }

            return result ?? AnalysisResult.CreateFailure(Name, "Analysis returned null result", stopwatch.Elapsed);
        }
        catch (OperationCanceledException) when (timeoutCts?.Token.IsCancellationRequested == true)
        {
            stopwatch.Stop();
            _logger.LogWarning(
                "Analyzer {Name} timed out after {Duration}ms",
                Name,
                stopwatch.ElapsedMilliseconds);

            return AnalysisResult.CreateFailure(
                Name,
                $"Analysis timed out after {Timeout.TotalSeconds} seconds",
                stopwatch.Elapsed);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            _logger.LogDebug("Analyzer {Name} was cancelled", Name);

            return AnalysisResult.CreateSkipped(Name, "Analysis was cancelled");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Analyzer {Name} encountered an error", Name);

            return AnalysisResult.CreateFailure(
                Name,
                $"Analysis failed: {ex.Message}",
                stopwatch.Elapsed);
        }
        finally
        {
            linkedCts?.Dispose();
            timeoutCts?.Dispose();
        }
    }

    /// <inheritdoc />
    public virtual Task<bool> CanAnalyzeAsync(AnalysisContext context)
    {
        // Default implementation - validate context is not null
        return Task.FromResult(context != null);
    }

    /// <summary>
    ///     Performs the actual analysis logic.
    /// </summary>
    /// <param name="context">The analysis context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The analysis result.</returns>
    protected abstract Task<AnalysisResult> PerformAnalysisAsync(
        AnalysisContext context,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Creates a successful result with a simple section fragment.
    /// </summary>
    protected AnalysisResult CreateSuccessResult(string title, string content, int order = 100)
    {
        var fragment = ReportFragment.CreateSection(title, content, order);
        return AnalysisResult.CreateSuccess(Name, fragment);
    }

    /// <summary>
    ///     Creates a warning result with a warning fragment.
    /// </summary>
    protected AnalysisResult CreateWarningResult(string title, string content, int order = 50)
    {
        var fragment = ReportFragment.CreateWarning(title, content, order);
        var result = new AnalysisResult(Name)
        {
            Success = true,
            Fragment = fragment,
            Severity = AnalysisSeverity.Warning
        };
        result.AddWarning(content);
        return result;
    }

    /// <summary>
    ///     Creates an error result with an error fragment.
    /// </summary>
    protected AnalysisResult CreateErrorResult(string title, string content, int order = 10)
    {
        var fragment = ReportFragment.CreateError(title, content, order);
        var result = new AnalysisResult(Name)
        {
            Success = true,
            Fragment = fragment,
            Severity = AnalysisSeverity.Error
        };
        result.AddError(content);
        return result;
    }

    /// <summary>
    ///     Logs a debug message.
    /// </summary>
    protected void LogDebug(string message, params object[] args)
    {
        _logger.LogDebug($"[{Name}] {message}", args);
    }

    /// <summary>
    ///     Logs an informational message.
    /// </summary>
    protected void LogInformation(string message, params object[] args)
    {
        _logger.LogInformation($"[{Name}] {message}", args);
    }

    /// <summary>
    ///     Logs a warning message.
    /// </summary>
    protected void LogWarning(string message, params object[] args)
    {
        _logger.LogWarning($"[{Name}] {message}", args);
    }

    /// <summary>
    ///     Logs an error message.
    /// </summary>
    protected void LogError(Exception? exception, string message, params object[] args)
    {
        _logger.LogError(exception, $"[{Name}] {message}", args);
    }
}