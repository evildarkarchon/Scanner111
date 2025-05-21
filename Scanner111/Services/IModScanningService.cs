using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Scanner111.Services
{
    /// <summary>
    /// Interface for scanning mod files (both unpacked and archived)
    /// </summary>
    public interface IModScanningService
    {
        /// <summary>
        /// Scans loose mod files for issues and moves redundant files to backup location.
        /// </summary>
        /// <returns>Detailed report of scan results.</returns>
        Task<string> ScanModsUnpackedAsync();

        /// <summary>
        /// Analyzes archived BA2 mod files to identify potential issues.
        /// </summary>
        /// <returns>A report detailing the findings, including errors and warnings.</returns>
        Task<string> ScanModsArchivedAsync();

        /// <summary>
        /// Combines the results of scanning unpacked and archived mods.
        /// </summary>
        /// <returns>The combined results of the unpacked and archived mods scans.</returns>
        Task<string> GetModsCombinedResultAsync();
    }
}
