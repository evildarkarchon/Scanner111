using System.Threading.Tasks;

namespace Scanner111.Core.GameScanning
{
    /// <summary>
    /// Interface for checking and validating Crash Generator (Buffout4) configuration.
    /// </summary>
    public interface ICrashGenChecker
    {
        /// <summary>
        /// Asynchronously checks Buffout4 configuration settings and detects conflicts.
        /// </summary>
        /// <returns>A report of configuration issues and recommendations.</returns>
        Task<string> CheckAsync();
        
        /// <summary>
        /// Checks if a specific plugin is installed.
        /// </summary>
        /// <param name="pluginNames">List of plugin names to check.</param>
        /// <returns>True if any of the plugins are installed.</returns>
        bool HasPlugin(List<string> pluginNames);
    }
}