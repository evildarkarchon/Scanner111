using System;
using System.Threading.Tasks;

namespace Scanner111.Services
{
    /// <summary>
    /// Implementation of IScanGameService that coordinates game and mod scanning functionality
    /// </summary>
    public class ScanGameService : IScanGameService
    {
        private readonly IGameFileManagementService _gameFileManagementService;
        private readonly IModScanningService _modScanningService;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScanGameService"/> class.
        /// </summary>
        /// <param name="gameFileManagementService">The game file management service.</param>
        /// <param name="modScanningService">The mod scanning service.</param>
        public ScanGameService(
            IGameFileManagementService gameFileManagementService,
            IModScanningService modScanningService)
        {
            _gameFileManagementService = gameFileManagementService ?? throw new ArgumentNullException(nameof(gameFileManagementService));
            _modScanningService = modScanningService ?? throw new ArgumentNullException(nameof(modScanningService));
        }

        /// <summary>
        /// Performs a complete scan of both game files and mods.
        /// </summary>
        /// <returns>A string containing the combined scan results.</returns>
        public async Task<string> PerformCompleteScanAsync()
        {
            var gameResults = await _gameFileManagementService.GetGameCombinedResultAsync();
            var modResults = await _modScanningService.GetModsCombinedResultAsync();
            return gameResults + modResults;
        }

        /// <summary>
        /// Scans only game files without scanning mods.
        /// </summary>
        /// <returns>A string containing the game scan results.</returns>
        public async Task<string> ScanGameFilesOnlyAsync()
        {
            return await _gameFileManagementService.GetGameCombinedResultAsync();
        }

        /// <summary>
        /// Scans only mod files without scanning game files.
        /// </summary>
        /// <returns>A string containing the mod scan results.</returns>
        public async Task<string> ScanModsOnlyAsync()
        {
            return await _modScanningService.GetModsCombinedResultAsync();
        }

        /// <summary>
        /// Manages game files (backup, restore, remove).
        /// </summary>
        /// <param name="classicList">The name of the list specifying which files need to be managed.</param>
        /// <param name="mode">The operation mode to be performed on the files (BACKUP, RESTORE, REMOVE).</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task ManageGameFilesAsync(string classicList, string mode = "BACKUP")
        {
            await _gameFileManagementService.GameFilesManageAsync(classicList, mode);
        }

        /// <summary>
        /// Writes the scan results to a markdown file.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task WriteReportAsync()
        {
            await _gameFileManagementService.WriteCombinedResultsAsync();
        }
    }
}
