namespace Scanner111.Common.Services.Logging;

/// <summary>
/// Provides paths for application log files.
/// </summary>
public interface ILogPathProvider
{
    /// <summary>
    /// Gets the directory where log files are stored.
    /// </summary>
    string GetLogDirectory();

    /// <summary>
    /// Gets the path pattern for the current log file.
    /// The pattern includes a placeholder for the rolling date suffix.
    /// </summary>
    string GetLogFilePath();

    /// <summary>
    /// Gets the path to today's log file (if it exists).
    /// </summary>
    string? GetCurrentLogFile();
}
