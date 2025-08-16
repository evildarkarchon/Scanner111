using System.Threading;
using System.Threading.Tasks;
using Scanner111.Core.Models;

namespace Scanner111.Core.GameScanning
{
    /// <summary>
    /// Interface for comprehensive game scanning functionality.
    /// </summary>
    public interface IGameScannerService
    {
        /// <summary>
        /// Performs a comprehensive game scan including all checks.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>Complete game scan results.</returns>
        Task<GameScanResult> ScanGameAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Performs only the Crash Generator check.
        /// </summary>
        Task<string> CheckCrashGenAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Performs only the XSE plugin validation.
        /// </summary>
        Task<string> ValidateXsePluginsAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Performs only the mod INI scan.
        /// </summary>
        Task<string> ScanModInisAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Performs only the Wrye Bash check.
        /// </summary>
        Task<string> CheckWryeBashAsync(CancellationToken cancellationToken = default);
    }
}