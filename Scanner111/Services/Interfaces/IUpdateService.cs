using System;
using System.Threading.Tasks;

namespace Scanner111.Services.Interfaces
{
    /// <summary>
    /// Interface for the update checking service that verifies if the current version
    /// of CLASSIC is the latest available.
    /// </summary>
    public interface IUpdateService
    {
        /// <summary>
        /// Asynchronously checks if the currently installed version is the latest available
        /// by comparing against sources defined in settings (GitHub, Nexus, or both).
        /// </summary>
        /// <param name="quiet">If true, suppresses detailed console output</param>
        /// <param name="guiRequest">If true, indicates the request is from the GUI and may raise exceptions instead of returning false</param>
        /// <returns>True if installed version is latest, false otherwise or if check failed</returns>
        /// <exception cref="UpdateCheckException">Thrown when there's an error checking for updates or a newer version is available (if guiRequest=true)</exception>
        Task<bool> IsLatestVersionAsync(bool quiet = false, bool guiRequest = true);
    }

    /// <summary>
    /// Exception thrown when an error occurs during update checking or a newer version is available
    /// </summary>
    public class UpdateCheckException : Exception
    {
        public UpdateCheckException(string message) : base(message) { }
        public UpdateCheckException(string message, Exception innerException) : base(message, innerException) { }
    }
}
