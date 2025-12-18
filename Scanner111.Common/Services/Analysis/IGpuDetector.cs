using Scanner111.Common.Models.Analysis;

namespace Scanner111.Common.Services.Analysis;

/// <summary>
/// Detects GPU information from crash log system specifications.
/// Extracts primary/secondary GPUs, manufacturer, and rival GPU for compatibility checks.
/// </summary>
public interface IGpuDetector
{
    /// <summary>
    /// Detects GPU information from the system specs segment of a crash log.
    /// </summary>
    /// <param name="systemSpecsSegment">The SYSTEM SPECS segment from the crash log.</param>
    /// <returns>
    /// GPU information including primary GPU, secondary GPU (if present),
    /// manufacturer, and rival manufacturer for compatibility checks.
    /// </returns>
    GpuInfo Detect(LogSegment? systemSpecsSegment);

    /// <summary>
    /// Detects GPU information from a collection of log segments.
    /// Automatically finds the SYSTEM SPECS segment.
    /// </summary>
    /// <param name="segments">All segments from the crash log.</param>
    /// <returns>
    /// GPU information including primary GPU, secondary GPU (if present),
    /// manufacturer, and rival manufacturer for compatibility checks.
    /// </returns>
    GpuInfo DetectFromSegments(IReadOnlyList<LogSegment> segments);
}
