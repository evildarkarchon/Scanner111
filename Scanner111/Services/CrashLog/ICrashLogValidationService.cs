// Scanner111/Services/CrashLog/ICrashLogValidationService.cs

using System.Collections.Generic;
using System.Threading.Tasks;
using Scanner111.Models.CrashLog;

namespace Scanner111.Services.CrashLog;

/// <summary>
/// Provides crash log validation and information extraction services
/// </summary>
public interface ICrashLogValidationService
{
    /// <summary>
    /// Validates whether a file is a supported crash log by checking its header
    /// </summary>
    /// <param name="filePath">Path to the file to validate</param>
    /// <returns>True if the file is a valid crash log, false otherwise</returns>
    Task<bool> IsValidCrashLogAsync(string filePath);

    /// <summary>
    /// Extracts crash log information from a file's header
    /// </summary>
    /// <param name="filePath">Path to the crash log file</param>
    /// <returns>Crash log information if valid, null otherwise</returns>
    Task<CrashLogInfo?> GetCrashLogInfoAsync(string filePath);

    /// <summary>
    /// Gets all supported game and crash generator combinations
    /// </summary>
    /// <returns>Collection of supported combinations</returns>
    IEnumerable<SupportedCombination> GetSupportedCombinations();

    /// <summary>
    /// Validates multiple files and returns information about valid ones
    /// </summary>
    /// <param name="filePaths">Collection of file paths to validate</param>
    /// <returns>Collection of valid crash log information</returns>
    Task<IEnumerable<CrashLogInfo>> GetValidCrashLogsAsync(IEnumerable<string> filePaths);
}