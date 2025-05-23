using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Scanner111.Models;

namespace Scanner111.Services.Interfaces;

/// <summary>
///     Main service for scanning crash logs.
///     Equivalent to Python's ClassicScanLogs class.
/// </summary>
public interface ICrashLogScanService : IDisposable
{
    /// <summary>
    ///     Initializes the scan service with settings and file cache.
    /// </summary>
    /// <returns>A task representing the initialization.</returns>
    Task InitializeAsync();

    /// <summary>
    ///     Processes all crash logs and returns results.
    /// </summary>
    /// <returns>A task containing the scan results.</returns>
    Task<(List<CrashLogProcessResult> Results, ScanStatistics Statistics)> ProcessAllCrashLogsAsync();

    /// <summary>
    ///     Processes a single crash log file.
    /// </summary>
    /// <param name="logFileName">Name of the log file to process.</param>
    /// <returns>A task containing the processing result.</returns>
    Task<CrashLogProcessResult> ProcessSingleCrashLogAsync(string logFileName);

    /// <summary>
    ///     Finds and extracts segments from crash log data.
    /// </summary>
    /// <param name="crashData">The crash log data lines.</param>
    /// <param name="crashgenName">Name of the crash generator.</param>
    /// <returns>Extracted segments from the crash log.</returns>
    Task<CrashLogSegments> FindSegmentsAsync(List<string> crashData, string crashgenName);

    /// <summary>
    ///     Gets the current scan statistics.
    /// </summary>
    /// <returns>Current scan statistics.</returns>
    ScanStatistics GetStatistics();
}