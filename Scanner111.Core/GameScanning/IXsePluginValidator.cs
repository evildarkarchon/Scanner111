using System.Threading.Tasks;

namespace Scanner111.Core.GameScanning
{
    /// <summary>
    /// Interface for validating XSE plugin compatibility and Address Library installation.
    /// </summary>
    public interface IXsePluginValidator
    {
        /// <summary>
        /// Asynchronously validates Address Library installation for the correct game version.
        /// </summary>
        /// <returns>A report of validation results and recommendations.</returns>
        Task<string> ValidateAsync();
    }
}