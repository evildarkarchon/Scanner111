namespace Scanner111.Core.GameScanning;

/// <summary>
///     Interface for scanning and analyzing mod INI configuration files.
/// </summary>
public interface IModIniScanner
{
    /// <summary>
    ///     Asynchronously scans mod INI files for problematic settings and applies fixes.
    /// </summary>
    /// <returns>A report of findings and applied fixes.</returns>
    Task<string> ScanAsync();
}