using System.Threading.Tasks;

namespace Scanner111.Services
{
    /// <summary>
    /// Service for scanning game files and mods
    /// </summary>
    public interface IScanGameService
    {
        /// <summary>
        /// Performs a complete scan of both game files and mods.
        /// </summary>
        /// <returns>A string containing the combined scan results.</returns>
        Task<string> PerformCompleteScanAsync();

        /// <summary>
        /// Scans only game files without scanning mods.
        /// </summary>
        /// <returns>A string containing the game scan results.</returns>
        Task<string> ScanGameFilesOnlyAsync();

        /// <summary>
        /// Scans only mod files without scanning game files.
        /// </summary>
        /// <returns>A string containing the mod scan results.</returns>
        Task<string> ScanModsOnlyAsync();

        /// <summary>
        /// Manages game files (backup, restore, remove).
        /// </summary>
        /// <param name="classicList">The name of the list specifying which files need to be managed.</param>
        /// <param name="mode">The operation mode to be performed on the files (BACKUP, RESTORE, REMOVE).</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ManageGameFilesAsync(string classicList, string mode = "BACKUP");

        /// <summary>
        /// Writes the scan results to a markdown file.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task WriteReportAsync();
    }
}
