using Scanner111.Common.Models.Analysis;
using Scanner111.Common.Models.Reporting;

namespace Scanner111.Common.Services.Analysis;

/// <summary>
/// Detects and evaluates mods using YAML mappings and crash log plugins.
/// This includes single mod detection, mod conflict detection, and important mod checking.
/// </summary>
public interface IModDetector
{
    /// <summary>
    /// Detects problematic mods based on plugin list from crash log.
    /// </summary>
    /// <param name="plugins">The plugins extracted from the crash log.</param>
    /// <param name="xseModules">XSE module names (DLL files) from F4SE PLUGINS section.</param>
    /// <param name="gpuType">The detected GPU type for compatibility checks (null if unknown).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The mod detection result containing all detected issues.</returns>
    Task<ModDetectionResult> DetectAsync(
        IReadOnlyList<PluginInfo> plugins,
        IReadOnlySet<string> xseModules,
        GpuType? gpuType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a report fragment for detected mods.
    /// </summary>
    /// <param name="result">The mod detection result.</param>
    /// <returns>A report fragment containing the mod detection findings.</returns>
    ReportFragment CreateReportFragment(ModDetectionResult result);
}

/// <summary>
/// Represents the detected GPU type for mod compatibility checking.
/// </summary>
public enum GpuType
{
    /// <summary>
    /// NVIDIA GPU.
    /// </summary>
    Nvidia,

    /// <summary>
    /// AMD GPU.
    /// </summary>
    Amd
}
