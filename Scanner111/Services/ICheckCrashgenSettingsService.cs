using System.Threading.Tasks;

namespace Scanner111.Services
{
    /// <summary>
    /// Interface for checking Buffout/Crashgen settings
    /// </summary>
    public interface ICheckCrashgenSettingsService
    {
        /// <summary>
        /// Checks Buffout/Crashgen settings for potential issues or optimizations.
        /// </summary>
        /// <returns>A detailed report of settings analysis.</returns>
        Task<string> CheckCrashgenSettingsAsync();
    }
}
