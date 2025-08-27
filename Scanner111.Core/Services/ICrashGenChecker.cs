namespace Scanner111.Core.Services;

/// <summary>
///     Service for checking and validating Crash Generator (Buffout4) configuration settings.
///     Provides functionality equivalent to Python's CheckCrashgen.py.
/// </summary>
public interface ICrashGenChecker
{
    /// <summary>
    ///     Performs comprehensive crash generator settings check and validation.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Report of configuration check results and any corrections made</returns>
    Task<string> CheckCrashGenSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Detects installed plugins in the specified plugins directory.
    /// </summary>
    /// <param name="pluginsPath">Path to the plugins directory</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Set of installed plugin names (lowercase)</returns>
    Task<IReadOnlySet<string>> DetectInstalledPluginsAsync(string pluginsPath, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks if any of the specified plugins are installed.
    /// </summary>
    /// <param name="pluginNames">Plugin names to check for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if any of the plugins are found</returns>
    Task<bool> HasPluginAsync(IEnumerable<string> pluginNames, CancellationToken cancellationToken = default);
}