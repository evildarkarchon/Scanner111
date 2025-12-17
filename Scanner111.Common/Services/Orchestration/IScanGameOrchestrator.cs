using Scanner111.Common.Models.ScanGame;

namespace Scanner111.Common.Services.Orchestration;

/// <summary>
/// Orchestrates comprehensive game and mod installation scanning operations.
/// </summary>
/// <remarks>
/// <para>
/// This orchestrator coordinates multiple independent scanners in parallel:
/// <list type="bullet">
/// <item>Unpacked (loose) mod file scanning</item>
/// <item>BA2 archive scanning</item>
/// <item>INI configuration validation</item>
/// <item>TOML crash generator configuration validation</item>
/// <item>XSE installation integrity checking</item>
/// <item>Game installation integrity checking</item>
/// </list>
/// </para>
/// <para>
/// Individual scanner failures do not prevent other scanners from completing.
/// All errors are captured in the <see cref="ScanGameResult.Errors"/> property.
/// </para>
/// </remarks>
public interface IScanGameOrchestrator
{
    /// <summary>
    /// Performs a comprehensive game installation scan using the specified configuration.
    /// </summary>
    /// <param name="configuration">The scan configuration specifying paths and options.</param>
    /// <param name="progress">Optional progress reporter for aggregate scan progress.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="ScanGameResult"/> containing all scan results and any errors.</returns>
    /// <remarks>
    /// <para>
    /// Scanners are run concurrently where possible. The scan continues even if
    /// individual scanners fail; all errors are captured in the result.
    /// </para>
    /// <para>
    /// Scanners are skipped if:
    /// <list type="bullet">
    /// <item>The corresponding scan option is disabled in configuration</item>
    /// <item>Required paths are null or empty</item>
    /// <item>Required sub-configuration is null (for XSE and GameIntegrity)</item>
    /// </list>
    /// </para>
    /// </remarks>
    Task<ScanGameResult> ScanAsync(
        ScanGameConfiguration configuration,
        IProgress<ScanGameProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a comprehensive scan and writes the report to a file.
    /// </summary>
    /// <param name="configuration">The scan configuration specifying paths and options.</param>
    /// <param name="reportPath">The path to write the markdown report file.</param>
    /// <param name="progress">Optional progress reporter for aggregate scan progress.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="ScanGameResult"/> containing all scan results and any errors.</returns>
    /// <remarks>
    /// <para>
    /// This method combines <see cref="ScanAsync"/> with report file writing.
    /// The report is written only if scanning produces content (i.e., the
    /// generated report has content).
    /// </para>
    /// </remarks>
    Task<ScanGameResult> ScanAndWriteReportAsync(
        ScanGameConfiguration configuration,
        string reportPath,
        IProgress<ScanGameProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
