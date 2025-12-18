namespace Scanner111.Common.Services.Logging;

/// <summary>
/// Provides paths for application log files in the user's LocalAppData folder.
/// </summary>
public class LogPathProvider : ILogPathProvider
{
    private const string AppFolder = "Scanner111";
    private const string LogsFolder = "Logs";
    private const string LogFilePrefix = "scanner111-";
    private const string LogFileExtension = ".log";

    private readonly string _logDirectory;

    public LogPathProvider()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _logDirectory = Path.Combine(localAppData, AppFolder, LogsFolder);
    }

    /// <inheritdoc />
    public string GetLogDirectory() => _logDirectory;

    /// <inheritdoc />
    public string GetLogFilePath()
    {
        // Serilog's RollingInterval.Day appends the date between the filename and extension
        // e.g., "scanner111-20231215.log"
        return Path.Combine(_logDirectory, $"{LogFilePrefix}{LogFileExtension}");
    }

    /// <inheritdoc />
    public string? GetCurrentLogFile()
    {
        if (!Directory.Exists(_logDirectory))
        {
            return null;
        }

        // Find today's log file (Serilog format: scanner111-yyyyMMdd.log)
        var today = DateTime.Now.ToString("yyyyMMdd");
        var expectedFileName = $"{LogFilePrefix}{today}{LogFileExtension}";
        var expectedPath = Path.Combine(_logDirectory, expectedFileName);

        return File.Exists(expectedPath) ? expectedPath : null;
    }
}
