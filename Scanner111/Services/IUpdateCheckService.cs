using System;
using System.Threading.Tasks;

namespace Scanner111.Services
{
    /// <summary>
    /// Interface for checking if the application is up to date by comparing
    /// local version with remote versions from GitHub and/or Nexus.
    /// </summary>
    public interface IUpdateCheckService
    {
        /// <summary>
        /// Checks if the current version of Scanner111 is the latest available version.
        /// </summary>
        /// <param name="quiet">If true, suppresses detailed output to logs.</param>
        /// <param name="guiRequest">Indicates if the request originates from the GUI.</param>
        /// <returns>True if the installed version is the latest; False otherwise.</returns>
        Task<bool> IsLatestVersionAsync(bool quiet = false, bool guiRequest = true);

        /// <summary>
        /// Parses a version string into a Version object.
        /// </summary>
        /// <param name="versionStr">The version string to parse.</param>
        /// <returns>A Version object or null if parsing fails.</returns>
        Version? TryParseVersion(string? versionStr);
    }
}
