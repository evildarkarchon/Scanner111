using Scanner111.Core.Models;

namespace Scanner111.Core.Analysis;

/// <summary>
///     Interface for mod detection analysis capabilities.
///     Provides methods to detect problematic mods, conflicts, and important missing mods.
/// </summary>
public interface IModDetectionAnalyzer : IAnalyzer
{
    /// <summary>
    ///     Detects problematic mods that can cause frequent crashes or issues.
    ///     Equivalent to Python's detect_mods_single function.
    /// </summary>
    /// <param name="crashLogPlugins">Dictionary of plugins found in crash log</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of detected mod warnings</returns>
    Task<IReadOnlyList<ModWarning>> DetectProblematicModsAsync(
        IReadOnlyDictionary<string, string> crashLogPlugins,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Detects conflicting mod combinations that should not be used together.
    ///     Equivalent to Python's detect_mods_double function.
    /// </summary>
    /// <param name="crashLogPlugins">Dictionary of plugins found in crash log</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of detected mod conflicts</returns>
    Task<IReadOnlyList<ModConflict>> DetectModConflictsAsync(
        IReadOnlyDictionary<string, string> crashLogPlugins,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Detects important/recommended mods and checks their installation status.
    ///     Equivalent to Python's detect_mods_important function.
    /// </summary>
    /// <param name="crashLogPlugins">Dictionary of plugins found in crash log</param>
    /// <param name="detectedGpuType">Detected GPU type for compatibility checking</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of important mods with their status</returns>
    Task<IReadOnlyList<ImportantMod>> DetectImportantModsAsync(
        IReadOnlyDictionary<string, string> crashLogPlugins,
        string? detectedGpuType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Performs comprehensive mod detection analysis including warnings, conflicts, and important mods.
    /// </summary>
    /// <param name="crashLogPlugins">Dictionary of plugins found in crash log</param>
    /// <param name="detectedGpuType">Detected GPU type for compatibility checking</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Complete mod detection settings with all detected information</returns>
    Task<ModDetectionSettings> PerformComprehensiveAnalysisAsync(
        IReadOnlyDictionary<string, string> crashLogPlugins,
        string? detectedGpuType = null,
        CancellationToken cancellationToken = default);
}