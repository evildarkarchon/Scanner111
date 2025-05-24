using System.Collections.Generic;
using System.Threading.Tasks;

namespace Scanner111.Services.Interfaces;

/// <summary>
///     Utility service for crash log file operations.
/// </summary>
public interface ICrashLogFileService
{
    /// <summary>
    ///     Gets all crash log files from the appropriate directories.
    /// </summary>
    /// <returns>A list of crash log file paths.</returns>
    Task<List<string>> GetCrashLogFilesAsync();

    /// <summary>
    ///     Reformats crash log files according to settings.
    /// </summary>
    /// <param name="crashLogFiles">List of crash log files to reformat.</param>
    /// <param name="removePatterns">Patterns of lines to remove.</param>
    /// <returns>A task representing the reformatting operation.</returns>
    Task ReformatCrashLogsAsync(IEnumerable<string> crashLogFiles, IEnumerable<string> removePatterns);

    /// <summary>
    ///     Moves files from source to target directory if they don't exist in target.
    /// </summary>
    /// <param name="sourceDir">Source directory.</param>
    /// <param name="targetDir">Target directory.</param>
    /// <param name="pattern">File pattern to match.</param>
    /// <returns>A task representing the move operation.</returns>
    Task MoveFilesAsync(string sourceDir, string targetDir, string pattern);

    /// <summary>
    ///     Copies files from source to target directory if they don't exist in target.
    /// </summary>
    /// <param name="sourceDir">Source directory.</param>
    /// <param name="targetDir">Target directory.</param>
    /// <param name="pattern">File pattern to match.</param>
    /// <returns>A task representing the copy operation.</returns>
    Task CopyFilesAsync(string sourceDir, string targetDir, string pattern);
}