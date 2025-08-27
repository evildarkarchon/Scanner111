using Scanner111.Core.Models;
using Scanner111.Core.Reporting;

namespace Scanner111.Core.Analysis;

/// <summary>
///     Interface for analyzing crash generator and mod settings.
/// </summary>
public interface ISettingsAnalyzer : IAnalyzer
{
    /// <summary>
    ///     Analyzes achievement settings for potential conflicts.
    /// </summary>
    Task<ReportFragment> ScanAchievementsSettingAsync(
        CrashGenSettings settings,
        ModDetectionSettings modSettings,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Analyzes memory management settings for conflicts and optimization.
    /// </summary>
    Task<ReportFragment> ScanMemoryManagementSettingsAsync(
        CrashGenSettings settings,
        ModDetectionSettings modSettings,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Analyzes the archive limit setting for stability issues.
    /// </summary>
    Task<ReportFragment> ScanArchiveLimitSettingAsync(
        CrashGenSettings settings,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Analyzes Looks Menu (F4EE) compatibility settings.
    /// </summary>
    Task<ReportFragment> ScanLooksMenuSettingAsync(
        CrashGenSettings settings,
        ModDetectionSettings modSettings,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks for potentially problematic disabled settings.
    /// </summary>
    Task<ReportFragment> CheckDisabledSettingsAsync(
        CrashGenSettings settings,
        CancellationToken cancellationToken = default);
}