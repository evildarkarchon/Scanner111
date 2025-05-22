using System;
using System.Threading;
using System.Threading.Tasks;
using Scanner111.Models;

namespace Scanner111.Services;

/// <summary>
///     Interface for game file management operations
/// </summary>
public interface IGameFileManagementService
{
    /// <summary>
    ///     Manages game files by performing backup, restore, or removal operations.
    /// </summary>
    /// <param name="classicList">The name of the list specifying which files need to be managed.</param>
    /// <param name="mode">The operation mode to be performed on the files (BACKUP, RESTORE, REMOVE).</param>
    /// <param name="progress">Optional progress reporter to receive progress updates.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation with a string result.</returns>
    Task<string> GameFilesManageAsync(string classicList, string mode = "BACKUP",
        IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Generates a combined result summarizing game-related checks and scans.
    /// </summary>
    /// <param name="progress">Optional progress reporter to receive progress updates.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A string summarizing the results of all performed checks and scans.</returns>
    Task<string> GetGameCombinedResultAsync(IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Writes combined results of game and mods into a markdown report file.
    /// </summary>
    /// <param name="progress">Optional progress reporter to receive progress updates.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task WriteCombinedResultsAsync(IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default);
}