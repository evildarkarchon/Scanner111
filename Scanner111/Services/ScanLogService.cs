using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Scanner111.Models;
using Scanner111.Services;

namespace Scanner111.Services
{
    /// <summary>
    /// Main service that coordinates scanning of log files
    /// </summary>    
    public class ScanLogService
    {
        private readonly AppSettings _appSettings;
        private readonly WarningDatabase _warningDatabase;
        private readonly CrashLogParserService _parserService;
        private readonly PluginDetectionService _pluginDetection;
        private readonly CrashAnalysisService _crashAnalysis;
        private readonly YamlSettingsCacheService _yamlSettingsCache;
        private readonly CrashStackAnalysis _crashStackAnalysis;
        private readonly CrashLogFormattingService _formattingService;

        public ScanLogService(
            AppSettings appSettings,
            WarningDatabase warningDatabase,
            CrashLogParserService parserService,
            PluginDetectionService pluginDetection,
            CrashAnalysisService crashAnalysis,
            YamlSettingsCacheService yamlSettingsCache,
            CrashStackAnalysis crashStackAnalysis,
            CrashLogFormattingService formattingService)
        {
            _appSettings = appSettings;
            _warningDatabase = warningDatabase;
            _parserService = parserService;
            _pluginDetection = pluginDetection;
            _crashAnalysis = crashAnalysis;
            _yamlSettingsCache = yamlSettingsCache;
            _crashStackAnalysis = crashStackAnalysis;
            _formattingService = formattingService;
        }

        /// <summary>
        /// Preprocesses and formats crash log files before scanning them
        /// </summary>
        /// <param name="logFilePaths">File paths to preprocess</param>
        /// <returns>Number of successfully preprocessed files</returns>
        public async Task<int> PreprocessCrashLogsAsync(IEnumerable<string> logFilePaths)
        {
            // Apply formatting to crash logs (normalize plugin load order, optionally remove noise)
            return await _formattingService.ReformatCrashLogsAsync(
                logFilePaths,
                _appSettings.SimplifyRemoveStrings
            );
        }

        /// <summary>
        /// Scans a single crash log file
        /// </summary>
        public async Task<List<LogIssue>> ScanLogFileAsync(string logFilePath)
        {
            var issues = new List<LogIssue>();
            if (!File.Exists(logFilePath))
            {
                issues.Add(new LogIssue
                {
                    FileName = Path.GetFileName(logFilePath),
                    Title = "File Not Found",
                    Message = $"The specified crash log file was not found: {logFilePath}",
                    Severity = SeverityLevel.Error
                });
                return issues;
            }

            // Format the crash log before parsing if needed
            await PreprocessCrashLogsAsync(new[] { logFilePath });

            // 1. Parse the crash log content
            var parsedLog = await _parserService.ParseCrashLogContentAsync(logFilePath);
            if (!parsedLog.Lines.Any()) // Check if parsing failed or file was empty/not found by parser
            {
                issues.Add(new LogIssue
                {
                    FileName = Path.GetFileName(logFilePath),
                    Title = "Log Parsing Failed",
                    Message = $"Could not parse or read content from: {logFilePath}",
                    Severity = SeverityLevel.Error
                });
                return issues;
            }

            // 2. Perform various checks based on parsedLog and settings/warningDatabase
            _pluginDetection.DetectModIssues(parsedLog, issues);
            _crashAnalysis.AnalyzeCrashLog(parsedLog, issues);

            // Fallback for unhandled exceptions during development of this service
            if (!issues.Any() && parsedLog.Lines.Any()) // If no specific issues found yet, but log was parsed
            {
                // This is just a placeholder to show the service was called.
                // Remove this once actual checks are implemented.
                bool unhandledExceptionFound = false;
                foreach (var line in parsedLog.Lines)
                {
                    if (line.Contains("Unhandled exception", StringComparison.OrdinalIgnoreCase))
                    {
                        unhandledExceptionFound = true;
                        break;
                    }
                }

                if (unhandledExceptionFound)
                {
                    issues.Add(new LogIssue
                    {
                        FileName = Path.GetFileName(logFilePath),
                        Title = "Generic Unhandled Exception",
                        Message = "An unhandled exception was mentioned in the log.",
                        Recommendation = "Review the log details for more information.",
                        Severity = SeverityLevel.Warning,
                        Source = "BasicScan"
                    });
                }
            }

            if (!issues.Any() && parsedLog.Lines.Any())
            {
                issues.Add(new LogIssue
                {
                    FileName = Path.GetFileName(logFilePath),
                    Title = "Scan Complete (No Specific Issues Identified)",
                    Message = "The log file was scanned, but no specific issues covered by current rules were found.",
                    Severity = SeverityLevel.Information,
                    Source = "ScanLogService"
                });
            }

            return issues;
        }

        /// <summary>
        /// Scans multiple crash log files.
        /// </summary>
        /// <param name="logFilePaths">A list of paths to crash log files.</param>
        /// <returns>A list of identified issues from all files.</returns>
        public async Task<List<LogIssue>> ScanMultipleLogFilesAsync(IEnumerable<string> logFilePaths)
        {
            // First preprocess all crash logs
            await PreprocessCrashLogsAsync(logFilePaths);

            // Then scan them
            var allIssues = new List<LogIssue>();
            foreach (var filePath in logFilePaths)
            {
                var fileIssues = await ScanLogFileAsync(filePath);
                allIssues.AddRange(fileIssues);
            }
            return allIssues;
        }
    }
}
