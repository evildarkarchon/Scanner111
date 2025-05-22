using System;
using System.Threading;
using System.Threading.Tasks;
using Scanner111.Models;

namespace Scanner111.Services;

/// <summary>
///     Interface for scanning mod files (both unpacked and archived)
/// </summary>
public interface IModScanningService
{
    /// <summary>
    ///     Scans loose mod files for issues and moves redundant files to backup location.
    /// </summary>
    /// <param name="progress">Optional progress reporter to receive progress updates.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>Detailed report of scan results.</returns>
    Task<string> ScanModsUnpackedAsync(IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Analyzes archived BA2 mod files to identify potential issues.
    /// </summary>
    /// <param name="progress">Optional progress reporter to receive progress updates.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A report detailing the findings, including errors and warnings.</returns>
    Task<string> ScanModsArchivedAsync(IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Combines the results of scanning unpacked and archived mods.
    /// </summary>
    /// <param name="progress">Optional progress reporter to receive progress updates.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>The combined results of the unpacked and archived mods scans.</returns>
    Task<string> GetModsCombinedResultAsync(IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Scans all mod files and returns a summary of the scan results.
    ///     This is a convenience method that calls the specialized scanning methods.
    /// </summary>
    /// <param name="progress">Optional progress reporter to receive progress updates.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A string containing the complete scan results summary.</returns>
    Task<string> ScanModsAsync(IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Clears the scan results cache to force fresh scans
    /// </summary>
    void ClearCache();
}