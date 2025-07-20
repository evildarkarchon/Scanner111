using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CliScanOptions = Scanner111.CLI.Models.ScanOptions;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;

namespace Scanner111.CLI.Services;

public class FileScanService : IFileScanService
{
    public async Task<FileScanData> CollectFilesToScanAsync(CliScanOptions options, ApplicationSettings settings)
    {
        var scanData = new FileScanData();
        
        // Auto-copy XSE crash logs first (F4SE and SKSE, similar to GUI functionality)
        await CopyXSELogsAsync(scanData, options, settings);
        
        // Add specific log file if provided
        if (!string.IsNullOrEmpty(options.LogFile) && File.Exists(options.LogFile))
        {
            scanData.FilesToScan.Add(options.LogFile);
            MessageHandler.MsgInfo($"Added log file: {Path.GetFileName(options.LogFile)}");
        }
        
        // Scan directory if provided
        if (!string.IsNullOrEmpty(options.ScanDir) && Directory.Exists(options.ScanDir))
        {
            var directory = new DirectoryInfo(options.ScanDir);
            var logs = directory.GetFiles("*.log", SearchOption.TopDirectoryOnly)
                .Concat(directory.GetFiles("*.txt", SearchOption.TopDirectoryOnly))
                .Where(f => f.Name.ToLower().Contains("crash") || f.Name.ToLower().Contains("dump"))
                .Select(f => f.FullName)
                .ToList();
                
            scanData.FilesToScan.AddRange(logs);
            MessageHandler.MsgInfo($"Found {logs.Count} crash logs in scan directory");
        }
        
        // If no specific files provided, scan current directory
        if (scanData.FilesToScan.Count == 0 && string.IsNullOrEmpty(options.LogFile) && string.IsNullOrEmpty(options.ScanDir))
        {
            var currentDir = new DirectoryInfo(Directory.GetCurrentDirectory());
            var logs = currentDir.GetFiles("crash-*.log", SearchOption.TopDirectoryOnly)
                .Concat(currentDir.GetFiles("crash-*.txt", SearchOption.TopDirectoryOnly))
                .Select(f => f.FullName)
                .ToList();
                
            scanData.FilesToScan.AddRange(logs);
            if (logs.Any())
            {
                MessageHandler.MsgInfo($"Found {logs.Count} crash logs in current directory");
            }
        }
        
        // Remove duplicates
        scanData.FilesToScan = scanData.FilesToScan.Distinct().ToList();
        
        return scanData;
    }

    private Task CopyXSELogsAsync(FileScanData scanData, CliScanOptions options, ApplicationSettings settings)
    {
        // Skip XSE copy if user disabled it
        if (options.SkipXSECopy)
        {
            return Task.CompletedTask;
        }
        
        try
        {
            // Get crash logs directory from settings or use default
            var crashLogsBaseDir = settings.CrashLogsDirectory;
            if (string.IsNullOrEmpty(crashLogsBaseDir))
            {
                crashLogsBaseDir = CrashLogDirectoryManager.GetDefaultCrashLogsDirectory();
            }

            // Look for XSE crash logs in common locations (F4SE and SKSE)
            var xsePaths = new[]
            {
                // F4SE paths
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Fallout4", "F4SE"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Fallout4VR", "F4SE"),
                // SKSE paths (including GOG version)
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Skyrim Special Edition", "SKSE"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Skyrim Special Edition GOG", "SKSE"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Skyrim", "SKSE"),
                // Also check game path if provided
                !string.IsNullOrEmpty(options.GamePath) ? Path.Combine(options.GamePath, "Data", "F4SE") : null,
                !string.IsNullOrEmpty(options.GamePath) ? Path.Combine(options.GamePath, "Data", "SKSE") : null
            }.Where(path => path != null && Directory.Exists(path)).ToArray();

            var copiedCount = 0;
            foreach (var xsePath in xsePaths)
            {
                if (Directory.Exists(xsePath))
                {
                    var crashLogs = Directory.GetFiles(xsePath!, "*.log", SearchOption.TopDirectoryOnly)
                        .Where(f => Path.GetFileName(f).StartsWith("crash-", StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(File.GetLastWriteTime)
                        .ToArray();

                    foreach (var logFile in crashLogs)
                    {
                        // Detect game type and copy to appropriate subdirectory
                        var gameType = CrashLogDirectoryManager.DetectGameType(options.GamePath, logFile);
                        var targetPath = CrashLogDirectoryManager.CopyCrashLog(logFile, crashLogsBaseDir, gameType, overwrite: true);
                        
                        var xseType = xsePath.Contains("F4SE") ? "F4SE" : "SKSE";
                        MessageHandler.MsgDebug($"Copied {xseType} {gameType} crash log: {Path.GetFileName(logFile)} -> {Path.GetDirectoryName(targetPath)}");
                        copiedCount++;
                        
                        if (!scanData.FilesToScan.Contains(targetPath))
                        {
                            scanData.FilesToScan.Add(targetPath);
                            scanData.XseCopiedFiles.Add(targetPath);
                        }
                    }
                }
            }

            if (copiedCount > 0)
            {
                MessageHandler.MsgSuccess($"Auto-copied {copiedCount} XSE crash logs");
            }
            else if (xsePaths.Length == 0)
            {
                MessageHandler.MsgDebug("No XSE directories found for auto-copy");
            }
            else
            {
                MessageHandler.MsgDebug("No new XSE crash logs to copy");
            }
        }
        catch (Exception ex)
        {
            MessageHandler.MsgWarning($"Error during XSE auto-copy: {ex.Message}");
        }

        return Task.CompletedTask;
    }
}