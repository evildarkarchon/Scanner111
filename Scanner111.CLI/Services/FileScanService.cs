using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using CliScanOptions = Scanner111.CLI.Models.ScanOptions;

namespace Scanner111.CLI.Services;

public class FileScanService : IFileScanService
{
    private readonly IMessageHandler _messageHandler;

    public FileScanService(IMessageHandler messageHandler)
    {
        _messageHandler = messageHandler ?? throw new ArgumentNullException(nameof(messageHandler));
    }
    /// <summary>
    /// Collects files to scan based on the specified scan options and application settings.
    /// </summary>
    /// <param name="options">The options that define scanning criteria, such as specific log file or directory to scan.</param>
    /// <param name="settings">The application settings containing configuration details for the scanning operation.</param>
    /// <returns>A task representing the asynchronous operation, containing the collected file scan data.</returns>
    public async Task<FileScanData> CollectFilesToScanAsync(CliScanOptions options, ApplicationSettings settings)
    {
        var scanData = new FileScanData();

        // Auto-copy XSE crash logs first (F4SE and SKSE, similar to GUI functionality)
        await CopyXseLogsAsync(scanData, options, settings).ConfigureAwait(false);

        // Add specific log file if provided
        if (!string.IsNullOrEmpty(options.LogFile) && File.Exists(options.LogFile))
        {
            scanData.FilesToScan.Add(options.LogFile);
            _messageHandler.ShowInfo($"Added log file: {Path.GetFileName(options.LogFile)}");
        }

        // Scan directory if provided
        if (!string.IsNullOrEmpty(options.ScanDir) && Directory.Exists(options.ScanDir))
        {
            var directory = new DirectoryInfo(options.ScanDir);
            var logs = directory.GetFiles("*.log", SearchOption.TopDirectoryOnly)
                .Concat(directory.GetFiles("*.txt", SearchOption.TopDirectoryOnly))
                .Where(f => f.Name.Contains("crash", StringComparison.CurrentCultureIgnoreCase) ||
                            f.Name.Contains("dump", StringComparison.CurrentCultureIgnoreCase))
                .Select(f => f.FullName)
                .ToList();

            scanData.FilesToScan.AddRange(logs);
            _messageHandler.ShowInfo($"Found {logs.Count} crash logs in scan directory");
        }

        // If no specific files provided, scan current directory
        if (scanData.FilesToScan.Count == 0 && string.IsNullOrEmpty(options.LogFile) &&
            string.IsNullOrEmpty(options.ScanDir))
        {
            var currentDir = new DirectoryInfo(Directory.GetCurrentDirectory());
            var logs = currentDir.GetFiles("crash-*.log", SearchOption.TopDirectoryOnly)
                .Concat(currentDir.GetFiles("crash-*.txt", SearchOption.TopDirectoryOnly))
                .Select(f => f.FullName)
                .ToList();

            scanData.FilesToScan.AddRange(logs);
            if (logs.Any()) _messageHandler.ShowInfo($"Found {logs.Count} crash logs in current directory");
        }

        // Remove duplicates
        scanData.FilesToScan = scanData.FilesToScan.Distinct().ToList();

        return scanData;
    }

    /// <summary>
    /// Copies XSE crash logs (such as F4SE and SKSE logs) from predefined directories to a designated scan data location.
    /// </summary>
    /// <param name="scanData">The object representing scan data where the logs will be added after copying.</param>
    /// <param name="options">The options specifying scan criteria, including whether XSE logs should be skipped or the game path for searching logs.</param>
    /// <param name="settings">The application settings containing configuration details, such as crash logs directory.</param>
    /// <returns>A task that represents the asynchronous operation of copying XSE logs.</returns>
    private Task CopyXseLogsAsync(FileScanData scanData, CliScanOptions options, ApplicationSettings settings)
    {
        // Skip XSE copy if user disabled it
        if (options.SkipXseCopy) return Task.CompletedTask;

        try
        {
            // Get crash logs directory from settings or use default
            var crashLogsBaseDir = settings.CrashLogsDirectory;
            if (string.IsNullOrEmpty(crashLogsBaseDir))
                crashLogsBaseDir = CrashLogDirectoryManager.GetDefaultCrashLogsDirectory();

            // Look for F4SE crash logs in common locations (prioritizing Fallout 4 only)
            var xsePaths = new[]
            {
                // F4SE paths only
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Fallout4",
                    "F4SE"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Fallout4VR",
                    "F4SE"),
                // Also check game path if provided
                !string.IsNullOrEmpty(options.GamePath) ? Path.Combine(options.GamePath, "Data", "F4SE") : null
            }.Where(path => path != null && Directory.Exists(path)).ToArray();

            var copiedCount = 0;
            foreach (var xsePath in xsePaths)
                if (Directory.Exists(xsePath))
                {
                    var crashLogs = Directory.GetFiles(xsePath, "*.log", SearchOption.TopDirectoryOnly)
                        .Where(f => Path.GetFileName(f).StartsWith("crash-", StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(File.GetLastWriteTime)
                        .ToArray();

                    foreach (var logFile in crashLogs)
                    {
                        // Detect game type and copy to appropriate subdirectory
                        var gameType = CrashLogDirectoryManager.DetectGameType(options.GamePath, logFile);
                        var targetPath =
                            CrashLogDirectoryManager.CopyCrashLog(logFile, crashLogsBaseDir, gameType);

                        var xseType = xsePath.Contains("SKSE") ? "SKSE" : "F4SE";
                        _messageHandler.ShowDebug(
                            $"Copied {xseType} {gameType} crash log: {Path.GetFileName(logFile)} -> {Path.GetDirectoryName(targetPath)}");
                        copiedCount++;

                        if (!scanData.FilesToScan.Contains(targetPath))
                        {
                            scanData.FilesToScan.Add(targetPath);
                            scanData.XseCopiedFiles.Add(targetPath);
                        }
                    }
                }

            if (copiedCount > 0)
                _messageHandler.ShowSuccess($"Auto-copied {copiedCount} XSE crash logs");
            else if (xsePaths.Length == 0)
                _messageHandler.ShowDebug("No XSE directories found for auto-copy");
            else
                _messageHandler.ShowDebug("No new XSE crash logs to copy");
        }
        catch (Exception ex)
        {
            _messageHandler.ShowWarning($"Error during XSE auto-copy: {ex.Message}");
        }

        return Task.CompletedTask;
    }
}