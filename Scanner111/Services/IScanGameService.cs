using System;
using System.Threading;
using System.Threading.Tasks;
using Scanner111.Models;

namespace Scanner111.Services;

/// <summary>
///     Service for scanning game files and mods
/// </summary>
public interface IScanGameService
{
    /// <summary>
    ///     Performs a complete scan of both game files and mods.
    /// </summary>
    /// <param name="progress">Optional progress reporter to receive progress updates.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A string containing the combined scan results.</returns>
    Task<string> PerformCompleteScanAsync(IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Scans only game files without scanning mods.
    /// </summary>
    /// <param name="progress">Optional progress reporter to receive progress updates.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A string containing the game scan results.</returns>
    Task<string> ScanGameFilesOnlyAsync(IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Scans only mod files without scanning game files.
    /// </summary>
    /// <param name="progress">Optional progress reporter to receive progress updates.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A string containing the mod scan results.</returns>
    Task<string> ScanModsOnlyAsync(IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Manages game files (backup, restore, remove).
    /// </summary>
    /// <param name="classicList">The name of the list specifying which files need to be managed.</param>
    /// <param name="mode">The operation mode to be performed on the files (BACKUP, RESTORE, REMOVE).</param>
    /// <param name="progress">Optional progress reporter to receive progress updates.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ManageGameFilesAsync(string classicList, string mode = "BACKUP", IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Writes the scan results to a markdown file.
    /// </summary>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task WriteReportAsync(CancellationToken cancellationToken = default);
}