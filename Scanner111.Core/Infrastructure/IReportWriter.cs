using Scanner111.Core.Models;

namespace Scanner111.Core.Infrastructure;

/// <summary>
/// Interface for writing scan reports to disk
/// </summary>
public interface IReportWriter
{
    /// <summary>
    /// Write a scan result report to the specified file path
    /// </summary>
    /// <param name="scanResult">The scan result to write</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the write was successful, false otherwise</returns>
    Task<bool> WriteReportAsync(ScanResult scanResult, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Write a scan result report to a custom file path
    /// </summary>
    /// <param name="scanResult">The scan result to write</param>
    /// <param name="outputPath">Custom output path for the report</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the write was successful, false otherwise</returns>
    Task<bool> WriteReportAsync(ScanResult scanResult, string outputPath, CancellationToken cancellationToken = default);
}