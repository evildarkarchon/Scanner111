using Microsoft.Extensions.Logging;
using Scanner111.Core.Models;
using Scanner111.Core.Reporting;
using Scanner111.Core.Services;

namespace Scanner111.Core.Analysis.Analyzers;

/// <summary>
/// Analyzes crash logs to extract GPU information and detect GPU-related compatibility issues.
/// Thread-safe analyzer that identifies graphics hardware for mod compatibility analysis.
/// </summary>
public sealed class GpuAnalyzer : AnalyzerBase
{
    private readonly IGpuDetector _gpuDetector;

    public GpuAnalyzer(
        ILogger<GpuAnalyzer> logger,
        IGpuDetector gpuDetector)
        : base(logger)
    {
        _gpuDetector = gpuDetector ?? throw new ArgumentNullException(nameof(gpuDetector));
    }

    /// <inheritdoc />
    public override string Name => "GpuAnalyzer";

    /// <inheritdoc />
    public override string DisplayName => "GPU Detection Analysis";

    /// <inheritdoc />
    public override int Priority => 15; // Run early to provide GPU info for other analyzers

    /// <inheritdoc />
    public override TimeSpan Timeout => TimeSpan.FromSeconds(10);

    /// <inheritdoc />
    protected override Task<AnalysisResult> PerformAnalysisAsync(
        AnalysisContext context,
        CancellationToken cancellationToken)
    {
        LogDebug("Starting GPU analysis for {Path}", context.InputPath);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Extract system specs segment from crash log
            if (!context.TryGetSharedData<List<string>>("SystemSpecsSegment", out var systemSpecsSegment) ||
                systemSpecsSegment == null || systemSpecsSegment.Count == 0)
            {
                LogDebug("No system specs segment found in context");
                return Task.FromResult(CreateNoSystemSpecsResult(context));
            }

            // Detect GPU information
            var gpuInfo = _gpuDetector.DetectGpuInfo(systemSpecsSegment);
            
            // Store GPU information in context for other analyzers (especially ModDetectionAnalyzer)
            context.SetSharedData("GpuInfo", gpuInfo);
            context.SetSharedData("DetectedGpuType", gpuInfo.Rival); // For backward compatibility

            cancellationToken.ThrowIfCancellationRequested();

            LogDebug("GPU detection completed: {GpuInfo}", gpuInfo.ToString());

            // Create report fragment
            var fragment = CreateGpuReportFragment(gpuInfo);

            var result = new AnalysisResult(Name)
            {
                Success = true,
                Fragment = fragment,
                Severity = DetermineGpuSeverity(gpuInfo)
            };

            LogDebug("GPU analysis completed successfully");
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            LogError(ex, "GPU analysis failed");
            var result = new AnalysisResult(Name)
            {
                Success = false,
                Fragment = ReportFragment.CreateError("GPU Analysis", 
                    "Failed to analyze GPU information from crash log.", 1000)
            };
            result.AddError(ex.Message);
            return Task.FromResult(result);
        }
    }

    /// <summary>
    /// Creates an analysis result when no system specs segment is available.
    /// </summary>
    private AnalysisResult CreateNoSystemSpecsResult(AnalysisContext context)
    {
        // Store unknown GPU info for other analyzers
        context.SetSharedData("GpuInfo", GpuInfo.Unknown);
        context.SetSharedData("DetectedGpuType", (string?)null);

        return new AnalysisResult(Name)
        {
            Success = true,
            Fragment = ReportFragment.CreateInfo("GPU Analysis", 
                "No system specifications found in crash log for GPU detection.", 1000)
        };
    }

    /// <summary>
    /// Creates a report fragment based on detected GPU information.
    /// </summary>
    private ReportFragment CreateGpuReportFragment(GpuInfo gpuInfo)
    {
        if (!gpuInfo.IsDetected)
        {
            return ReportFragment.CreateWarning("GPU Analysis",
                "Could not detect GPU information from crash log system specifications.", 800);
        }

        var lines = new List<string>
        {
            "### GPU Information\n",
            $"**Primary GPU:** {gpuInfo.Primary}\n",
            $"**Manufacturer:** {gpuInfo.Manufacturer}\n"
        };

        if (!string.IsNullOrEmpty(gpuInfo.Secondary))
        {
            lines.Add($"**Secondary GPU:** {gpuInfo.Secondary}\n");
        }

        // Add compatibility note if applicable
        if (!string.IsNullOrEmpty(gpuInfo.Rival))
        {
            lines.Add("\n");
            lines.Add($"*Note: This information will be used to check compatibility with {gpuInfo.Rival.ToUpperInvariant()}-specific mods.*\n");
        }

        lines.Add("\n");

        return ReportFragment.CreateInfo("GPU Analysis", string.Join("", lines), 200);
    }

    /// <summary>
    /// Determines the severity level based on GPU detection results.
    /// </summary>
    private static AnalysisSeverity DetermineGpuSeverity(GpuInfo gpuInfo)
    {
        if (!gpuInfo.IsDetected)
        {
            return AnalysisSeverity.Warning; // Warning level - couldn't detect GPU
        }

        return AnalysisSeverity.Info; // Info level - GPU detected successfully
    }
}