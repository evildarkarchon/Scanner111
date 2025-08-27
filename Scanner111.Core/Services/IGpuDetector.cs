using Scanner111.Core.Models;

namespace Scanner111.Core.Services;

/// <summary>
/// Service for detecting and parsing GPU information from crash log system specifications.
/// Provides GPU manufacturer identification and compatibility checking for mod analysis.
/// </summary>
public interface IGpuDetector
{
    /// <summary>
    /// Extracts GPU information from a system specification segment.
    /// </summary>
    /// <param name="systemSpecSegment">List of system specification lines from crash log.</param>
    /// <returns>GpuInfo containing detected GPU details, or Unknown if detection fails.</returns>
    GpuInfo DetectGpuInfo(IReadOnlyList<string> systemSpecSegment);

    /// <summary>
    /// Determines if the detected GPU is compatible with a specific mod based on GPU requirements.
    /// </summary>
    /// <param name="gpuInfo">The detected GPU information.</param>
    /// <param name="modWarning">The mod warning text to check for GPU-specific requirements.</param>
    /// <returns>True if compatible, false if there's a GPU compatibility issue.</returns>
    bool IsGpuCompatible(GpuInfo gpuInfo, string modWarning);
}