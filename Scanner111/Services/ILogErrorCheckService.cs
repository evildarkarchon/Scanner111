using System.IO;
using System.Threading.Tasks;

namespace Scanner111.Services
{
    /// <summary>
    /// Service for checking errors in log files
    /// </summary>
    public interface ILogErrorCheckService
    {
        /// <summary>
        /// Inspects log files within a specified folder for recorded errors.
        /// </summary>
        /// <param name="folderPath">Path to the folder containing log files for error inspection.</param>
        /// <returns>A detailed report of all detected errors in the relevant log files, if any.</returns>
        Task<string> CheckLogErrorsAsync(DirectoryInfo folderPath);

        /// <summary>
        /// Scans game logs for errors based on game directory specified in settings.
        /// </summary>
        /// <returns>A detailed report of all detected errors in the game log files, if any.</returns>
        Task<string> ScanGameLogsAsync();
    }
}
