namespace Scanner111.Core.Services;

/// <summary>
///     Service for checking XSE (Script Extender) plugin compatibility and Address Library versions.
///     Provides functionality equivalent to Python's CheckXsePlugins.py.
/// </summary>
public interface IXsePluginChecker
{
    /// <summary>
    ///     Checks XSE plugins for compatibility and Address Library version requirements.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Report detailing compatibility check results</returns>
    Task<string> CheckXsePluginsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Determines if the correct Address Library version is installed based on game version and VR mode.
    /// </summary>
    /// <param name="pluginsPath">Path to the plugins directory</param>
    /// <param name="gameVersion">Detected game version</param>
    /// <param name="isVrMode">Whether VR mode is enabled</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tuple indicating if correct version exists and any status message</returns>
    Task<(bool IsCorrectVersion, string Message)> ValidateAddressLibraryAsync(
        string pluginsPath, 
        string gameVersion,
        bool isVrMode, 
        CancellationToken cancellationToken = default);
}