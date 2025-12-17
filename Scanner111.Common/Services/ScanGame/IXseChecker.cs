using Scanner111.Common.Models.ScanGame;

namespace Scanner111.Common.Services.ScanGame;

/// <summary>
/// Provides functionality for checking the integrity and installation status
/// of XSE (Script Extender) and related components.
/// </summary>
/// <remarks>
/// <para>
/// This service validates the XSE installation by checking:
/// <list type="bullet">
/// <item>Address Library installation status</item>
/// <item>XSE log file existence and contents</item>
/// <item>XSE version against the latest known version</item>
/// <item>Error patterns in the XSE log file</item>
/// <item>Script file integrity via SHA-256 hash verification</item>
/// </list>
/// </para>
/// <para>
/// The checker operates in read-only mode and never modifies any files.
/// All detected issues are reported for the user to address manually.
/// </para>
/// </remarks>
public interface IXseChecker
{
    /// <summary>
    /// Performs a complete XSE integrity check.
    /// </summary>
    /// <param name="configuration">The XSE configuration containing paths and expected values.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An <see cref="XseScanResult"/> containing all check results.</returns>
    Task<XseScanResult> CheckIntegrityAsync(
        XseConfiguration configuration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks only the Address Library installation status.
    /// </summary>
    /// <param name="addressLibraryPath">The path to the Address Library file.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A tuple containing the installation status and detailed status enum.</returns>
    Task<(bool IsInstalled, AddressLibraryStatus Status)> CheckAddressLibraryAsync(
        string? addressLibraryPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks only the XSE installation and version.
    /// </summary>
    /// <param name="logFilePath">The path to the XSE log file.</param>
    /// <param name="latestVersion">The latest known XSE version string.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A tuple containing installation status, detected version, and whether it's the latest.</returns>
    Task<(XseInstallationStatus Status, string? DetectedVersion, bool IsLatest)> CheckXseInstallationAsync(
        string? logFilePath,
        string latestVersion,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Scans the XSE log file for error patterns.
    /// </summary>
    /// <param name="logFilePath">The path to the XSE log file.</param>
    /// <param name="errorPatterns">The error patterns to search for.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of errors found in the log file.</returns>
    Task<IReadOnlyList<XseLogError>> ScanLogForErrorsAsync(
        string logFilePath,
        IReadOnlyList<string> errorPatterns,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies script file hashes against expected values.
    /// </summary>
    /// <param name="scriptsFolderPath">The path to the Scripts folder.</param>
    /// <param name="expectedHashes">Dictionary of filename to expected SHA-256 hash.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of hash verification results for each script file.</returns>
    Task<IReadOnlyList<ScriptHashResult>> VerifyScriptHashesAsync(
        string scriptsFolderPath,
        IReadOnlyDictionary<string, string> expectedHashes,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents progress information during an XSE integrity check.
/// </summary>
/// <param name="CurrentOperation">Description of the current operation.</param>
/// <param name="PercentComplete">Progress percentage (0-100).</param>
public record XseCheckProgress(
    string CurrentOperation,
    int PercentComplete);
