using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Scanner111.Models;

namespace Scanner111.Services;

/// <summary>
///     Implementation of IScanGameService that coordinates game and mod scanning functionality
/// </summary>
public class ScanGameService : IScanGameService
{
    private readonly IGameFileManagementService _gameFileManagementService;
    private readonly ILogger<ScanGameService>? _logger;
    private readonly IModScanningService _modScanningService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ScanGameService" /> class.
    /// </summary>
    /// <param name="gameFileManagementService">The game file management service.</param>
    /// <param name="modScanningService">The mod scanning service.</param>
    /// <param name="logger">Optional logger for diagnostic information.</param>
    public ScanGameService(
        IGameFileManagementService gameFileManagementService,
        IModScanningService modScanningService,
        ILogger<ScanGameService>? logger = null)
    {
        _gameFileManagementService = gameFileManagementService ??
                                     throw new ArgumentNullException(nameof(gameFileManagementService));
        _modScanningService = modScanningService ?? throw new ArgumentNullException(nameof(modScanningService));
        _logger = logger;
    }

    /// <summary>
    ///     Performs a complete scan of both game files and mods.
    /// </summary>
    /// <param name="progress">Optional progress reporter to receive progress updates.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A string containing the combined scan results.</returns>
    public async Task<string> PerformCompleteScanAsync(IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Starting complete scan of game files and mods");
        progress?.Report(new ScanProgress(0, "Starting complete scan"));

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Report progress at the start of game files scan
            progress?.Report(new ScanProgress(10, "Scanning game files"));

            // Scan game files (50% of total progress)
            var gameResults = await Task.Run(async () => await _gameFileManagementService.GetGameCombinedResultAsync()
                .ConfigureAwait(false), cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            // Report progress at the start of mod files scan
            progress?.Report(new ScanProgress(50, "Scanning mod files"));

            // Scan mod files (50% of total progress)
            var modResults = await Task.Run(async () => await _modScanningService.GetModsCombinedResultAsync()
                .ConfigureAwait(false), cancellationToken).ConfigureAwait(false);

            // Report completion
            progress?.Report(new ScanProgress(100, "Scan completed"));

            _logger?.LogInformation("Complete scan finished successfully");
            return gameResults + modResults;
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("Complete scan was cancelled");
            progress?.Report(new ScanProgress(0, "Scan cancelled"));
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during complete scan");
            progress?.Report(new ScanProgress(0, $"Error: {ex.Message}"));
            throw;
        }
    }

    /// <summary>
    ///     Scans only game files without scanning mods.
    /// </summary>
    /// <param name="progress">Optional progress reporter to receive progress updates.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A string containing the game scan results.</returns>
    public async Task<string> ScanGameFilesOnlyAsync(IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Starting game files only scan");
        progress?.Report(new ScanProgress(0, "Starting game files scan"));

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Offload to background thread to avoid blocking UI
            var result = await Task.Run(async () =>
            {
                // Report progress at 25%
                progress?.Report(new ScanProgress(25, "Scanning game files"));

                var gameResults = await _gameFileManagementService.GetGameCombinedResultAsync()
                    .ConfigureAwait(false);

                // Report completion
                progress?.Report(new ScanProgress(100, "Game files scan completed"));

                return gameResults;
            }, cancellationToken).ConfigureAwait(false);

            _logger?.LogInformation("Game files scan completed successfully");
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("Game files scan was cancelled");
            progress?.Report(new ScanProgress(0, "Scan cancelled"));
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during game files scan");
            progress?.Report(new ScanProgress(0, $"Error: {ex.Message}"));
            throw;
        }
    }

    /// <summary>
    ///     Scans only mod files without scanning game files.
    /// </summary>
    /// <param name="progress">Optional progress reporter to receive progress updates.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A string containing the mod scan results.</returns>
    public async Task<string> ScanModsOnlyAsync(IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Starting mods only scan");
        progress?.Report(new ScanProgress(0, "Starting mods scan"));

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Offload to background thread to avoid blocking UI
            var result = await Task.Run(async () =>
            {
                // Report progress at 25%
                progress?.Report(new ScanProgress(25, "Scanning mod files"));

                var modResults = await _modScanningService.GetModsCombinedResultAsync()
                    .ConfigureAwait(false);

                // Report completion
                progress?.Report(new ScanProgress(100, "Mods scan completed"));

                return modResults;
            }, cancellationToken).ConfigureAwait(false);

            _logger?.LogInformation("Mods scan completed successfully");
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("Mods scan was cancelled");
            progress?.Report(new ScanProgress(0, "Scan cancelled"));
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during mods scan");
            progress?.Report(new ScanProgress(0, $"Error: {ex.Message}"));
            throw;
        }
    }

    /// <summary>
    ///     Manages game files (backup, restore, remove).
    /// </summary>
    /// <param name="classicList">The name of the list specifying which files need to be managed.</param>
    /// <param name="mode">The operation mode to be performed on the files (BACKUP, RESTORE, REMOVE).</param>
    /// <param name="progress">Optional progress reporter to receive progress updates.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ManageGameFilesAsync(string classicList, string mode = "BACKUP",
        IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Starting game files management. Mode: {Mode}, List: {List}", mode, classicList);
        progress?.Report(new ScanProgress(0, $"Starting {mode.ToLower()} operation", classicList));

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Offload to background thread to avoid blocking UI
            await Task.Run(async () =>
            {
                // Report progress at 25%
                progress?.Report(new ScanProgress(25, $"Processing {mode.ToLower()} operation", classicList));

                await _gameFileManagementService
                    .GameFilesManageAsync(classicList, mode, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                // Report completion
                progress?.Report(new ScanProgress(100, $"{mode} operation completed", classicList));
            }, cancellationToken).ConfigureAwait(false);

            _logger?.LogInformation("Game files management completed successfully. Mode: {Mode}, List: {List}", mode,
                classicList);
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("Game files management was cancelled. Mode: {Mode}, List: {List}", mode,
                classicList);
            progress?.Report(new ScanProgress(0, "Operation cancelled"));
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during game files management. Mode: {Mode}, List: {List}", mode, classicList);
            progress?.Report(new ScanProgress(0, $"Error: {ex.Message}"));
            throw;
        }
    }

    /// <summary>
    ///     Writes the scan results to a markdown file.
    /// </summary>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task WriteReportAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Starting to write scan report");

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Offload to background thread to avoid blocking UI
            await Task.Run(async () =>
            {
                await _gameFileManagementService.WriteCombinedResultsAsync(cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);

            _logger?.LogInformation("Scan report written successfully");
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("Writing scan report was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error writing scan report");
            throw;
        }
    }
}