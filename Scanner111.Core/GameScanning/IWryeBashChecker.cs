using System.Threading.Tasks;

namespace Scanner111.Core.GameScanning
{
    /// <summary>
    /// Interface for analyzing Wrye Bash plugin checker reports.
    /// </summary>
    public interface IWryeBashChecker
    {
        /// <summary>
        /// Asynchronously analyzes Wrye Bash plugin checker report for issues.
        /// </summary>
        /// <returns>A detailed analysis of the plugin checker report.</returns>
        Task<string> AnalyzeAsync();
    }
}